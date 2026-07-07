using System.Text.RegularExpressions;
using MediatR;
using Microsoft.Extensions.Logging;
using Secop.Application.Interfaces;
using Secop.Domain.Constants;
using Secop.Domain.Entities;
using Secop.Domain.Enums;
using Secop.Domain.Services;
using Secop.Domain.ValueObjects;

namespace Secop.Application.UseCases.Procesos.SincronizarProcesos;

public class SincronizarProcesosHandler : IRequestHandler<SincronizarProcesosCommand, int>
{
    private readonly ISecopApiClient _secopApi;
    private readonly IProcesoRepository _procesos;
    private readonly IProveedorRepository _proveedores;
    private readonly IPuntajeRepository _puntajes;
    private readonly IEmbeddingService _embedding;
    private readonly IScoringService _scoring;
    private readonly IAlertaService _alertas;
    private readonly ILogger<SincronizarProcesosHandler> _logger;

    private const float UmbralSimilitudMinima = 0.40f;
    private const float UmbralAlertaProponer = 40f;

    public SincronizarProcesosHandler(
        ISecopApiClient secopApi,
        IProcesoRepository procesos,
        IProveedorRepository proveedores,
        IPuntajeRepository puntajes,
        IEmbeddingService embedding,
        IScoringService scoring,
        IAlertaService alertas,
        ILogger<SincronizarProcesosHandler> logger)
    {
        _secopApi = secopApi;
        _procesos = procesos;
        _proveedores = proveedores;
        _puntajes = puntajes;
        _embedding = embedding;
        _scoring = scoring;
        _alertas = alertas;
        _logger = logger;
    }

    public async Task<int> Handle(SincronizarProcesosCommand request, CancellationToken ct)
    {
        var todosProveedores = (await _proveedores.ObtenerTodosAsync(ct)).ToList();

        var codigosExactos = todosProveedores
            .SelectMany(p => p.CodigosUnspsc)
            .Distinct()
            .ToList();

        var codigosClase = codigosExactos
            .Select(c => new CodigoUnspsc(c).CodigoClase)
            .Distinct()
            .ToList();

        var tareaUnspsc = _secopApi.ObtenerProcesosRecientesAsync(request.Desde, request.Hasta, codigosExactos, codigosClase, ct);
        var tareaRecientes = _secopApi.ObtenerProcesosRecientesAsync(request.Desde, request.Hasta, ct: ct);

        await Task.WhenAll(tareaUnspsc, tareaRecientes);

        _logger.LogInformation(
            "SECOP debug — crudos UNSPSC: {UnspscCount}, crudos recientes: {RecientesCount}",
            tareaUnspsc.Result.Count,
            tareaRecientes.Result.Count);

        var dtosUnspscValidos = tareaUnspsc.Result
            .Where(d => d.EsValido())
            .ToList();

        _logger.LogInformation(
            "SECOP debug — válidos UNSPSC tras EsValido(): {ValidosUnspscCount}",
            dtosUnspscValidos.Count);

        var dtosUnspscAplicables = dtosUnspscValidos
            .Where(d => d.EstaAbiertoParaAplicar())
            .Where(d => !DebeDescartarse(d))
            .ToList();

        _logger.LogInformation(
            "SECOP debug — aplicables UNSPSC tras estado/fase: {AplicablesUnspscCount}",
            dtosUnspscAplicables.Count);

        var idsUnspsc = dtosUnspscAplicables
            .Select(d => d.Id!)
            .ToHashSet(StringComparer.OrdinalIgnoreCase);

        var todasPalabrasClave = todosProveedores
            .SelectMany(p => p.PalabrasClave)
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToList();

        var dtosKeywordOnly = todasPalabrasClave.Count > 0
            ? tareaRecientes.Result
                .Where(d => d.EsValido() && d.EstaAbiertoParaAplicar() && !idsUnspsc.Contains(d.Id!))
                .Where(d => !DebeDescartarse(d))
                .Where(d => ProcesoCoincideConAlgunaPalabraClave(todasPalabrasClave, d.Objeto))
                .GroupBy(d => d.Id!)
                .Select(g => g.First())
                .ToList()
            : [];

        _logger.LogInformation(
            "SECOP debug — palabras clave globales: {KeywordsCount}, candidatos keyword-only: {KeywordOnlyCount}",
            todasPalabrasClave.Count,
            dtosKeywordOnly.Count);

        var candidatosPorProceso = new Dictionary<string, CandidatoProceso>(StringComparer.OrdinalIgnoreCase);

        foreach (var dto in dtosUnspscAplicables.Concat(dtosKeywordOnly))
        {
            foreach (var proveedor in todosProveedores)
            {
                if (proveedor.Embedding is null) continue;
                if (!ProcesoCoincideConPalabrasClave(proveedor, dto.Objeto)) continue;

                if (!candidatosPorProceso.TryGetValue(dto.Id!, out var candidato))
                {
                    candidato = new CandidatoProceso(dto, !idsUnspsc.Contains(dto.Id!));
                    candidatosPorProceso[dto.Id!] = candidato;
                }

                candidato.Proveedores.Add(proveedor);
            }
        }

        var candidatos = candidatosPorProceso.Values.ToList();

        _logger.LogInformation(
            "SECOP debug — candidatos únicos por proveedor antes de embeddings: {Count}",
            candidatos.Count);

        int nuevos = 0;

        foreach (var candidato in candidatos)
        {
            var dto = candidato.Dto;

            if (await _procesos.ExisteAsync(dto.Id!, ct))
                continue;

            var proceso = MapearProceso(dto);

            var textoEmbedding = $"{proceso.Titulo} {proceso.Objeto}";
            var embeddingVector = await _embedding.GenerarEmbeddingAsync(textoEmbedding, ct);
            proceso.AsignarEmbedding(embeddingVector);

            await _procesos.GuardarAsync(proceso, ct);
            nuevos++;

            foreach (var proveedor in candidato.Proveedores)
            {
                var similitud = await _embedding.CalcularSimilitudAsync(embeddingVector, proveedor.Embedding);

                _logger.LogInformation(
                    "Similitud entre Proceso {ProcesoId} y Proveedor {ProveedorId}: {Similitud}",
                    proceso.Id, proveedor.Id, similitud);

                if (similitud < UmbralSimilitudMinima) continue;

                var puntaje = await _scoring.CalcularAsync(proveedor, proceso, similitud);

                if (candidato.EsKeywordOnly)
                    puntaje.AgregarAdvertencia(AdvertenciasPuntaje.EncontradoPorTexto);

                if (DetectorRegimenEspecial.EsPosibleRegimenEspecial(proceso.NombreEntidad))
                    puntaje.AgregarAdvertencia(AdvertenciasPuntaje.PosibleRegimenEspecial);

                await _puntajes.GuardarAsync(puntaje, ct);

                if (puntaje.PuntajeTotal >= UmbralAlertaProponer && proveedor.TelegramChatId.HasValue)
                {
                    await _alertas.EnviarAlertaProcesoAsync(
                        proveedor.TelegramChatId.Value, puntaje, proceso, proveedor, ct);
                }
            }

            _logger.LogInformation("Proceso ingresado: {Id} | {Titulo}", proceso.Id, proceso.Titulo);
        }

        _logger.LogInformation("Sincronización completada. Procesos nuevos: {Nuevos}", nuevos);
        return nuevos;
    }

    /// <summary>
    /// Descarta siempre los procesos de Régimen Especial y de RFI (decisión de negocio):
    /// solo los procesos Ley80 se tienen en cuenta para posible postulación.
    /// </summary>
    private static bool DebeDescartarse(global::Secop.Application.DTOs.SecopProcesoDto dto) =>
        dto.ObtenerClasificacion() is ClasificacionRegimen.RegimenEspecial or ClasificacionRegimen.Rfi;

    private static Proceso MapearProceso(global::Secop.Application.DTOs.SecopProcesoDto dto)
    {
        _ = decimal.TryParse(dto.Presupuesto, out var presupuesto);
        _ = DateTime.TryParse(dto.FechaCierre, out var fechaCierre);
        _ = DateTime.TryParse(dto.FechaUltimaPublicacion, out var fechaPublicacion);
        fechaCierre = DateTime.SpecifyKind(fechaCierre, DateTimeKind.Utc);
        fechaPublicacion = DateTime.SpecifyKind(fechaPublicacion, DateTimeKind.Utc);

        decimal? valorAdjudicacion = decimal.TryParse(dto.ValorTotalAdjudicacion, out var valor) ? valor : null;
        DateTime? fechaAdjudicacion = DateTime.TryParse(dto.FechaAdjudicacion, out var fechaAdj)
            ? DateTime.SpecifyKind(fechaAdj, DateTimeKind.Utc)
            : null;

        return new Proceso(
            id: dto.Id!,
            titulo: dto.Titulo ?? string.Empty,
            objeto: dto.Objeto ?? string.Empty,
            presupuesto: presupuesto,
            fechaCierre: fechaCierre,
            fechaPublicacion: fechaPublicacion,
            modalidad: dto.ObtenerModalidad(),
            estado: dto.ObtenerEstado(),
            nombreEntidad: dto.NombreEntidad ?? string.Empty,
            nitEntidad: dto.NitEntidad ?? string.Empty,
            departamentoEntidad: dto.DepartamentoEntidad ?? string.Empty,
            urlProceso: dto.UrlProceso ?? string.Empty,
            clasificacion: dto.ObtenerClasificacion(),
            tipoContrato: dto.TipoContrato,
            adjudicadoA: dto.NombreProveedorAdjudicado,
            valorAdjudicacion: valorAdjudicacion,
            fechaAdjudicacion: fechaAdjudicacion,
            categoriasAdicionales: dto.ObtenerCategoriasAdicionales(),
            proveedoresInvitados: ParsearEntero(dto.ProveedoresInvitados),
            proveedoresQueManifestaron: ParsearEntero(dto.ProveedoresQueManifestaron),
            respuestasAlProcedimiento: ParsearEntero(dto.RespuestasAlProcedimiento),
            conteoRespuestasOfertas: ParsearEntero(dto.ConteoRespuestasOfertas),
            proveedoresUnicosCon: ParsearEntero(dto.ProveedoresUnicosCon)
        );
    }

    private static int? ParsearEntero(string? valor) =>
        int.TryParse(valor, out var resultado) ? resultado : null;

    private static bool ProcesoCoincideConPalabrasClave(Proveedor proveedor, string? objetoProceso)
    {
        var tienePalabrasClaveValidas = false;

        foreach (var palabraClave in proveedor.PalabrasClave)
        {
            if (string.IsNullOrWhiteSpace(palabraClave))
                continue;

            tienePalabrasClaveValidas = true;

            if (ContienePalabraClave(objetoProceso, palabraClave))
                return true;
        }

        return !tienePalabrasClaveValidas;
    }

    private static bool ProcesoCoincideConAlgunaPalabraClave(IEnumerable<string> palabrasClave, string? objetoProceso)
    {
        foreach (var palabraClave in palabrasClave)
        {
            if (string.IsNullOrWhiteSpace(palabraClave))
                continue;

            if (ContienePalabraClave(objetoProceso, palabraClave))
                return true;
        }

        return false;
    }

    private static bool ContienePalabraClave(string? texto, string palabraClave)
    {
        if (string.IsNullOrWhiteSpace(texto)) return false;

        var keyword = palabraClave.Trim();
        if (keyword.Length == 0) return false;

        // Acrónimos o keywords cortas como "IA", "BTL", "ATL" no pueden usar Contains,
        // porque producen falsos positivos dentro de palabras como "asistencial" o "social".
        if (keyword.Length <= 3)
        {
            var pattern = $@"(?<![\p{{L}}\p{{N}}]){Regex.Escape(keyword)}(?![\p{{L}}\p{{N}}])";
            return Regex.IsMatch(texto, pattern, RegexOptions.IgnoreCase | RegexOptions.CultureInvariant);
        }

        return texto.Contains(keyword, StringComparison.OrdinalIgnoreCase);
    }

    private sealed class CandidatoProceso
    {
        public CandidatoProceso(global::Secop.Application.DTOs.SecopProcesoDto dto, bool esKeywordOnly)
        {
            Dto = dto;
            EsKeywordOnly = esKeywordOnly;
        }

        public global::Secop.Application.DTOs.SecopProcesoDto Dto { get; }
        public bool EsKeywordOnly { get; }
        public HashSet<Proveedor> Proveedores { get; } = [];
    }
}
