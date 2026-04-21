using MediatR;
using Microsoft.Extensions.Logging;
using Secop.Application.Interfaces;

namespace Secop.Application.UseCases.Puntajes.RecalcularPuntajes;

public class RecalcularPuntajesHandler : IRequestHandler<RecalcularPuntajesCommand, int>
{
    private readonly IProcesoRepository _procesos;
    private readonly IProveedorRepository _proveedores;
    private readonly IPuntajeRepository _puntajes;
    private readonly IEmbeddingService _embedding;
    private readonly IScoringService _scoring;
    private readonly ILogger<RecalcularPuntajesHandler> _logger;

    private const float UmbralSimilitudMinima = 0.65f;

    public RecalcularPuntajesHandler(
        IProcesoRepository procesos,
        IProveedorRepository proveedores,
        IPuntajeRepository puntajes,
        IEmbeddingService embedding,
        IScoringService scoring,
        ILogger<RecalcularPuntajesHandler> logger)
    {
        _procesos    = procesos;
        _proveedores = proveedores;
        _puntajes    = puntajes;
        _embedding   = embedding;
        _scoring     = scoring;
        _logger      = logger;
    }

    public async Task<int> Handle(RecalcularPuntajesCommand request, CancellationToken ct)
    {
        var procesosActivos = (await _procesos.ObtenerActivosAsync(ct))
            .Where(p => p.EstaVigente() && p.Embedding is not null)
            .ToList();

        var proveedoresTarget = request.SoloProveedorId.HasValue
            ? [(await _proveedores.ObtenerPorIdAsync(request.SoloProveedorId.Value, ct))!]
            : (await _proveedores.ObtenerTodosAsync(ct)).ToArray();

        int recalculados = 0;

        foreach (var proveedor in proveedoresTarget.Where(p => p?.Embedding is not null))
        {
            foreach (var proceso in procesosActivos)
            {
                var similitud = await _embedding.CalcularSimilitudAsync(proceso.Embedding!, proveedor.Embedding!);
                if (similitud < UmbralSimilitudMinima) continue;

                var puntaje = await _scoring.CalcularAsync(proveedor, proceso, similitud);
                await _puntajes.GuardarAsync(puntaje, ct);
                recalculados++;
            }
        }

        _logger.LogInformation("Puntajes recalculados: {Total}", recalculados);
        return recalculados;
    }
}
