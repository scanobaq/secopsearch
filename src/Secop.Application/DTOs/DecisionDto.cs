using Secop.Domain.Enums;

namespace Secop.Application.DTOs;

public class DecisionDto
{
    public string ProcesoId { get; init; } = null!;
    public Guid ProveedorId { get; init; }
    public AccionDecision Accion { get; init; }
    public string? RazonDescarte { get; init; }
}
