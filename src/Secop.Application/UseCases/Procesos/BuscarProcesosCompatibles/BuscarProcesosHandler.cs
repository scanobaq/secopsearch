using MediatR;
using Secop.Application.DTOs;
using Secop.Application.Interfaces;
using Secop.Domain.Constants;
using Secop.Domain.Entities;

namespace Secop.Application.UseCases.Procesos.BuscarProcesosCompatibles;

public class BuscarProcesosHandler : IRequestHandler<BuscarProcesosQuery, List<PuntajeDto>>
{
    private readonly IProveedorRepository _proveedores;
    private readonly IProcesoRepository _procesos;
    private readonly IPuntajeRepository _puntajes;

    public BuscarProcesosHandler(
        IProveedorRepository proveedores,
        IProcesoRepository procesos,
        IPuntajeRepository puntajes)
    {
        _proveedores = proveedores;
        _procesos = procesos;
        _puntajes = puntajes;
    }

    public async Task<List<PuntajeDto>> Handle(BuscarProcesosQuery request, CancellationToken ct)
    {
        IEnumerable<Proveedor> proveedoresTarget = request.ProveedorId.HasValue
            ? [(await _proveedores.ObtenerPorIdAsync(request.ProveedorId.Value, ct))!]
            : await _proveedores.ObtenerTodosAsync(ct);

        proveedoresTarget = proveedoresTarget.Where(p => p is not null);

        var resultado = new List<PuntajeDto>();

        foreach (var proveedor in proveedoresTarget)
        {
            var evaluaciones = await _puntajes.ObtenerPorProveedorAsync(proveedor.Id, ct);
            foreach (var evaluacion in evaluaciones.Where(e =>
                         e.RelevanciaPorcentaje >= PoliticaEvaluacion.UmbralRelevanciaPorcentaje &&
                         e.EsAlertable))
            {
                var proceso = await _procesos.ObtenerPorIdAsync(evaluacion.ProcesoId, ct);
                if (proceso is not null)
                    resultado.Add(PuntajeDto.Desde(evaluacion, proceso, proveedor));
            }
        }

        return [.. resultado
            .OrderByDescending(p => p.RelevanciaPorcentaje)
            .Take(request.Limite)];
    }
}
