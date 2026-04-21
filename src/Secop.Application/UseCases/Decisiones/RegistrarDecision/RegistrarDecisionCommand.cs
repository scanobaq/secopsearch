using MediatR;
using Secop.Domain.Enums;

namespace Secop.Application.UseCases.Decisiones.RegistrarDecision;

public record RegistrarDecisionCommand(
    string ProcesoId,
    Guid ProveedorId,
    AccionDecision Accion,
    string? RazonDescarte = null) : IRequest;
