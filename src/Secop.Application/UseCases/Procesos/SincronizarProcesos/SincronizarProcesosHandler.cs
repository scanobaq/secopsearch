using System.Collections;
using MediatR;
using Microsoft.Extensions.Logging;
using Secop.Application.Interfaces;
using Secop.Domain.Constants;
using Secop.Domain.Entities;
using Secop.Domain.Enums;
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

    private const float UmbralSimilitudMinima = 0.65f;
    private const float UmbralAlertaProponer = 70f;

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

        var dtosUnspscValidos = tareaUnspsc.Result.Where(d => d.EsValido()).ToList();
        var idsUnspsc = dtosUnspscValidos
            .Select(d => d.Id!)
            .ToHashSet(StringComparer.OrdinalIgnoreCase);

        var todasPalabrasClave = todosProveedores
            .SelectMany(p => p.PalabrasClave)
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToList();

        var dtosKeywordOnly = todasPalabrasClave.Count > 0
            ? tareaRecientes.Result
                .Where(d => d.EsValido() && !idsUnspsc.Contains(d.Id!))
                .Where(d => todasPalabrasClave.Any(kw =>
                    d.Objeto?.Contains(kw, StringComparison.OrdinalIgnoreCase) == true))
                .GroupBy(d => d.Id!)
                .Select(g => g.First())
                .ToList()
            : [];

        var todosDtos = dtosUnspscValidos.Concat(dtosKeywordOnly).ToList();
        _logger.LogInformation("Proccess finding: {count}", todosDtos.Count());

        int nuevos = 0;

        foreach (var dto in todosDtos)
        {
            if (await _procesos.ExisteAsync(dto.Id!, ct))
                continue;

            var proceso = MapearProceso(dto);

            var textoEmbedding = $"{proceso.Titulo} {proceso.Objeto}";
            var embeddingVector = await _embedding.GenerarEmbeddingAsync(textoEmbedding, ct);
            proceso.AsignarEmbedding(embeddingVector);

            await _procesos.GuardarAsync(proceso, ct);
            nuevos++;

            bool esKeywordOnly = !idsUnspsc.Contains(dto.Id!);

            foreach (var proveedor in todosProveedores)
            {
                if (proveedor.Embedding is null) continue;

                if (proveedor.PalabrasClave.Count > 0 &&
                    !proveedor.PalabrasClave.Any(kw =>
                        !string.IsNullOrWhiteSpace(kw) &&
                        dto.Objeto?.Contains(kw, StringComparison.OrdinalIgnoreCase) == true))
                    continue;

                var similitud = await _embedding.CalcularSimilitudAsync(embeddingVector, proveedor.Embedding);

                if (similitud < UmbralSimilitudMinima) continue;

                var puntaje = await _scoring.CalcularAsync(proveedor, proceso, similitud);

                if (esKeywordOnly)
                    puntaje.AgregarAdvertencia(AdvertenciasPuntaje.EncontradoPorTexto);

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

    private static Proceso MapearProceso(global::Secop.Application.DTOs.SecopProcesoDto dto)
    {
        _ = decimal.TryParse(dto.Presupuesto, out var presupuesto);
        _ = DateTime.TryParse(dto.FechaCierre, out var fechaCierre);
        _ = DateTime.TryParse(dto.FechaUltimaPublicacion, out var fechaPublicacion);

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
            urlProceso: dto.UrlProceso ?? string.Empty
        );
    }
}
