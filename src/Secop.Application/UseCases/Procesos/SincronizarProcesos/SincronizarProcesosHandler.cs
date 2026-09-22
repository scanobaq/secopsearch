using System.Globalization;
using MediatR;
using Microsoft.Extensions.Logging;
using Secop.Application.Interfaces;
using Secop.Application.Services;
using Secop.Domain.Constants;
using Secop.Domain.Entities;
using Secop.Domain.Enums;
using Secop.Domain.Services;

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
        var proveedoresConEmbedding = (await _proveedores.ObtenerTodosAsync(ct))
            .Where(proveedor => proveedor.Embedding is not null)
            .ToList();
        var procesosRecientes = await _secopApi.ObtenerProcesosRecientesAsync(
            request.Desde,
            request.Hasta,
            ct: ct);
        var procesosAplicables = procesosRecientes
            .Where(dto => dto.EsValido())
            .Where(dto => dto.EstaAbiertoParaAplicar())
            .Where(dto => !DebeDescartarse(dto))
            .GroupBy(dto => dto.Id!, StringComparer.OrdinalIgnoreCase)
            .Select(grupo => grupo.First())
            .ToList();

        var evaluados = 0;
        var rechazadosPorSimilitud = 0;
        var nuevos = 0;
        var puntajesGuardados = 0;
        var intentosAlerta = 0;
        var maximumSimilaritiesByProcess = new List<string>();

        foreach (var dto in procesosAplicables)
        {
            if (await _procesos.ExisteAsync(dto.Id!, ct))
                continue;

            var modalidad = dto.ObtenerModalidad();
            decimal? presupuestoCop = dto.TryObtenerPresupuestoCop(out var presupuesto) ? presupuesto : null;
            if (!ElegibilidadProceso.EsElegible(modalidad, presupuestoCop))
                continue;

            evaluados++;
            var proceso = MapearProceso(dto, presupuesto, modalidad);
            var textoEmbedding = ConstructorTextoSemantico.CrearParaProceso(proceso);
            var embeddingVector = await _embedding.GenerarEmbeddingAsync(textoEmbedding, ct);
            proceso.AsignarEmbedding(embeddingVector);

            var proveedoresCalificados = new List<(Proveedor Proveedor, float Similitud)>();
            float? maximumSimilarity = null;
            foreach (var proveedor in proveedoresConEmbedding)
            {
                var similitud = await _embedding.CalcularSimilitudAsync(embeddingVector, proveedor.Embedding!);
                if (!maximumSimilarity.HasValue || similitud > maximumSimilarity.Value)
                    maximumSimilarity = similitud;

                if (PoliticaEvaluacion.Admitir(
                        similitud,
                        proveedor.CodigosUnspsc,
                        proceso.CodigoPrincipalCategoria,
                        proceso.CategoriasAdicionales))
                    proveedoresCalificados.Add((proveedor, similitud));
            }

            var maximumSimilarityText = maximumSimilarity.HasValue
                ? maximumSimilarity.Value.ToString("F4", CultureInfo.InvariantCulture)
                : "N/A";
            maximumSimilaritiesByProcess.Add(
                $"{NormalizeIdForLog(dto.Id!)}={maximumSimilarityText}");

            if (proveedoresCalificados.Count == 0)
            {
                rechazadosPorSimilitud++;
                continue;
            }

            await _procesos.GuardarAsync(proceso, ct);
            nuevos++;

            foreach (var (proveedor, similitud) in proveedoresCalificados)
            {
                var puntaje = await _scoring.CalcularAsync(proveedor, proceso, similitud);
                if (DetectorRegimenEspecial.EsPosibleRegimenEspecial(proceso.NombreEntidad))
                    puntaje.AgregarRazon(RazonesEvaluacion.PosibleRegimenEspecial);

                await _puntajes.GuardarAsync(puntaje, ct);
                puntajesGuardados++;

                if (puntaje.EsAlertable && proveedor.TelegramChatId.HasValue)
                {
                    intentosAlerta++;
                    await _alertas.EnviarAlertaProcesoAsync(
                        proveedor.TelegramChatId.Value, puntaje, proceso, proveedor, ct);
                }
            }
        }

        var similaritySummary = maximumSimilaritiesByProcess.Count == 0
            ? "ninguna"
            : string.Join(", ", maximumSimilaritiesByProcess);
        _logger.LogInformation(
            "Sincronización completada. Elegibles evaluados: {Evaluados}; rechazados sin proveedor sobre {Umbral:0.00}: {Rechazados}; procesos persistidos: {Persistidos}; puntajes guardados: {Puntajes}; intentos de alerta: {Alertas}; similitudes máximas por proceso: {MaxSimilaritiesByProcess}",
            evaluados,
            PoliticaEvaluacion.UmbralSimilitud,
            rechazadosPorSimilitud,
            nuevos,
            puntajesGuardados,
            intentosAlerta,
            similaritySummary);
        return nuevos;
    }

    private static string NormalizeIdForLog(string id) =>
        id.Replace("\r\n", " ").Replace('\r', ' ').Replace('\n', ' ');

    /// <summary>
    /// Descarta siempre los procesos de Régimen Especial y de RFI (decisión de negocio):
    /// solo los procesos Ley80 se tienen en cuenta para posible postulación.
    /// </summary>
    private static bool DebeDescartarse(global::Secop.Application.DTOs.SecopProcesoDto dto) =>
        dto.ObtenerClasificacion() is ClasificacionRegimen.RegimenEspecial or ClasificacionRegimen.Rfi;

    private static Proceso MapearProceso(
        global::Secop.Application.DTOs.SecopProcesoDto dto,
        decimal presupuesto,
        ModalidadContrato modalidad)
    {
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
            modalidad: modalidad,
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
            proveedoresUnicosCon: ParsearEntero(dto.ProveedoresUnicosCon),
            codigoPrincipalCategoria: dto.ObtenerCodigoPrincipalCategoria()
        );
    }

    private static int? ParsearEntero(string? valor) =>
        int.TryParse(valor, out var resultado) ? resultado : null;

}
