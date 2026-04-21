using MediatR;
using Secop.Application.DTOs;
using Secop.Application.Interfaces;
using Secop.Domain.Entities;
using Secop.Domain.Enums;

namespace Secop.Application.UseCases.Procesos.BuscarProcesosCompatibles;

public class BuscarProcesosHandler : IRequestHandler<BuscarProcesosQuery, List<PuntajeDto>>
{
    private readonly IProveedorRepository _proveedores;
    private readonly IProcesoRepository _procesos;
    private readonly IScoringService _scoring;
    private readonly IEmbeddingService _embedding;

    private const float UmbralSimilitudMinima = 0.65f;

    public BuscarProcesosHandler(
        IProveedorRepository proveedores,
        IProcesoRepository procesos,
        IScoringService scoring,
        IEmbeddingService embedding)
    {
        _proveedores = proveedores;
        _procesos    = procesos;
        _scoring     = scoring;
        _embedding   = embedding;
    }

    public async Task<List<PuntajeDto>> Handle(BuscarProcesosQuery request, CancellationToken ct)
    {
        IEnumerable<Proveedor> proveedoresTarget = request.ProveedorId.HasValue
            ? [(await _proveedores.ObtenerPorIdAsync(request.ProveedorId.Value, ct))!]
            : await _proveedores.ObtenerTodosAsync(ct);

        proveedoresTarget = proveedoresTarget.Where(p => p?.Embedding is not null);

        var resultado = new List<PuntajeDto>();

        foreach (var proveedor in proveedoresTarget)
        {
            var similares = await _procesos.BuscarPorSimilitudAsync(
                proveedor.Embedding!, UmbralSimilitudMinima, request.Limite, ct);

            foreach (var (proceso, similitud) in similares.Where(x => x.Proceso.EstaVigente()))
            {
                var puntaje = await _scoring.CalcularAsync(proveedor, proceso, similitud);

                if (puntaje.Etiqueta == EtiquetaProceso.Descartar) continue;

                resultado.Add(MapearDto(puntaje, proceso, proveedor));
            }
        }

        return [.. resultado.OrderByDescending(p => p.PuntajeTotal)];
    }

    private static PuntajeDto MapearDto(Puntaje puntaje, Proceso proceso, Proveedor proveedor) =>
        new()
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
