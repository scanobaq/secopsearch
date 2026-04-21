using MediatR;
using Secop.Application.DTOs;

namespace Secop.Application.UseCases.Procesos.ObtenerDetalleProceso;

public record ObtenerDetalleQuery(string ProcesoId, Guid? ProveedorId = null) : IRequest<PuntajeDto?>;
