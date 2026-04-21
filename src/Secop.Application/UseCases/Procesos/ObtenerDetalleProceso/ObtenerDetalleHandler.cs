using MediatR;
using Secop.Application.DTOs;
using Secop.Application.Interfaces;
using Secop.Domain.Entities;

namespace Secop.Application.UseCases.Procesos.ObtenerDetalleProceso;

public class ObtenerDetalleHandler : IRequestHandler<ObtenerDetalleQuery, PuntajeDto?>
{
    private readonly IProcesoRepository _procesos;
    private readonly IProveedorRepository _proveedores;
    private readonly IScoringService _scoring;
    private readonly IEmbeddingService _embedding;

    private const float UmbralSimilitudMinima = 0.65f;

    public ObtenerDetalleHandler(
        IProcesoRepository procesos,
        IProveedorRepository proveedores,
        IScoringService scoring,
        IEmbeddingService embedding)
    {
        _procesos    = procesos;
        _proveedores = proveedores;
        _scoring     = scoring;
        _embedding   = embedding;
    }

    public async Task<PuntajeDto?> Handle(ObtenerDetalleQuery request, CancellationToken ct)
    {
        var proceso = await _procesos.ObtenerPorIdAsync(request.ProcesoId, ct);
        if (proceso is null || proceso.Embedding is null) return null;

        // Si no se especifica proveedor, tomar el de mayor afinidad
        Proveedor? proveedor = null;
        float mejorSimilitud = 0f;

        var todos = request.ProveedorId.HasValue
            ? [(await _proveedores.ObtenerPorIdAsync(request.ProveedorId.Value, ct))!]
            : (await _proveedores.ObtenerTodosAsync(ct)).ToArray();

        foreach (var p in todos.Where(p => p?.Embedding is not null))
        {
            var sim = await _embedding.CalcularSimilitudAsync(proceso.Embedding, p.Embedding!);
            if (sim > mejorSimilitud)
            {
                mejorSimilitud = sim;
                proveedor = p;
            }
        }

        if (proveedor is null || mejorSimilitud < UmbralSimilitudMinima) return null;

        var puntaje = await _scoring.CalcularAsync(proveedor, proceso, mejorSimilitud);

        return new PuntajeDto
        {
            Id                   = puntaje.Id,
            ProcesoId            = proceso.Id,
            ProcesoTitulo        = proceso.Titulo,
            NombreEntidad        = proceso.NombreEntidad,
            Presupuesto          = proceso.Presupuesto,
            FechaCierre          = proceso.FechaCierre,
            DiasHabilesRestantes = proceso.DiasHabilesRestantes(),
            UrlProceso           = proceso.UrlProceso,
            ProveedorId          = proveedor.Id,
            ProveedorNombre      = proveedor.Nombre,
            PuntajeTotal         = puntaje.PuntajeTotal,
            PuntajeSimilitud     = puntaje.PuntajeSimilitud,
            PuntajeRequisitos    = puntaje.PuntajeRequisitos,
            PuntajeTiempo        = puntaje.PuntajeTiempo,
            PuntajeCompetencia   = puntaje.PuntajeCompetencia,
            PuntajeEntidad       = puntaje.PuntajeEntidad,
            Etiqueta             = puntaje.Etiqueta,
            Advertencias         = puntaje.Advertencias,
            EsInhabilitado       = puntaje.EsInhabilitado
        };
    }
}
