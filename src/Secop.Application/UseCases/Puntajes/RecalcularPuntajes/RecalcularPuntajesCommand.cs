using MediatR;

namespace Secop.Application.UseCases.Puntajes.RecalcularPuntajes;

/// <summary>
/// Recalcula puntajes para todos los procesos activos vigentes.
/// Útil cuando se actualiza el perfil de un proveedor o se ajustan los pesos del scoring.
/// </summary>
public record RecalcularPuntajesCommand(Guid? SoloProveedorId = null) : IRequest<int>;
