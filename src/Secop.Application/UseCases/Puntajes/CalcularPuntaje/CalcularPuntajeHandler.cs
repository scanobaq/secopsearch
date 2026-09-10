using MediatR;
using Secop.Application.DTOs;
using Secop.Application.Interfaces;
using Secop.Domain.Constants;

namespace Secop.Application.UseCases.Puntajes.CalcularPuntaje;

public class CalcularPuntajeHandler : IRequestHandler<CalcularPuntajeCommand, PuntajeDto?>
{
    private readonly IProcesoRepository _procesos;
    private readonly IProveedorRepository _proveedores;
    private readonly IPuntajeRepository _puntajes;
    private readonly IEmbeddingService _embedding;
    private readonly IScoringService _scoring;

    public CalcularPuntajeHandler(
        IProcesoRepository procesos,
        IProveedorRepository proveedores,
        IPuntajeRepository puntajes,
        IEmbeddingService embedding,
        IScoringService scoring)
    {
        _procesos = procesos;
        _proveedores = proveedores;
        _puntajes = puntajes;
        _embedding = embedding;
        _scoring = scoring;
    }

    public async Task<PuntajeDto?> Handle(CalcularPuntajeCommand request, CancellationToken ct)
    {
        var proceso = await _procesos.ObtenerPorIdAsync(request.ProcesoId, ct);
        var proveedor = await _proveedores.ObtenerPorIdAsync(request.ProveedorId, ct);

        if (proceso is null || proveedor is null) return null;
        if (proceso.Embedding is null || proveedor.Embedding is null) return null;

        var similitud = await _embedding.CalcularSimilitudAsync(proceso.Embedding, proveedor.Embedding);
        if (similitud < PoliticaEvaluacion.UmbralSimilitud) return null;

        var puntaje = await _scoring.CalcularAsync(proveedor, proceso, similitud);

        await _puntajes.GuardarAsync(puntaje, ct);

        return PuntajeDto.Desde(puntaje, proceso, proveedor);
    }
}
