using MediatR;
using Secop.Application.DTOs;
using Secop.Application.Interfaces;
using Secop.Domain.Constants;

namespace Secop.Application.UseCases.Procesos.ObtenerDetalleProceso;

public class ObtenerDetalleHandler : IRequestHandler<ObtenerDetalleQuery, PuntajeDto?>
{
    private readonly IProcesoRepository _procesos;
    private readonly IProveedorRepository _proveedores;
    private readonly IPuntajeRepository _puntajes;

    public ObtenerDetalleHandler(
        IProcesoRepository procesos,
        IProveedorRepository proveedores,
        IPuntajeRepository puntajes)
    {
        _procesos = procesos;
        _proveedores = proveedores;
        _puntajes = puntajes;
    }

    public async Task<PuntajeDto?> Handle(ObtenerDetalleQuery request, CancellationToken ct)
    {
        var proceso = await _procesos.ObtenerPorIdAsync(request.ProcesoId, ct);
        if (proceso is null) return null;

        var evaluacion = request.ProveedorId.HasValue
            ? await _puntajes.ObtenerAsync(proceso.Id, request.ProveedorId.Value, ct)
            : (await _puntajes.ObtenerPorProcesoAsync(proceso.Id, ct)).FirstOrDefault();
        if (evaluacion is null ||
            evaluacion.RelevanciaPorcentaje < PoliticaEvaluacion.UmbralRelevanciaPorcentaje)
            return null;

        var proveedor = await _proveedores.ObtenerPorIdAsync(evaluacion.ProveedorId, ct);
        return proveedor is null ? null : PuntajeDto.Desde(evaluacion, proceso, proveedor);
    }
}
