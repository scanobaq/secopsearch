using MediatR;
using Secop.Application.DTOs;

namespace Secop.Application.UseCases.Procesos.BuscarProcesosCompatibles;

/// <summary>
/// Busca los procesos más compatibles para un proveedor específico usando similitud vectorial.
/// Si <see cref="ProveedorId"/> es null, busca para todos los proveedores.
/// </summary>
public record BuscarProcesosQuery(Guid? ProveedorId = null, int Limite = 20) : IRequest<List<PuntajeDto>>;
