using MediatR;
using Secop.Application.Interfaces;
using Secop.Domain.Entities;

namespace Secop.Application.UseCases.Decisiones.RegistrarDecision;

public class RegistrarDecisionHandler : IRequestHandler<RegistrarDecisionCommand>
{
    private readonly IDecisionRepository _decisiones;

    public RegistrarDecisionHandler(IDecisionRepository decisiones)
    {
        _decisiones = decisiones;
    }

    public async Task Handle(RegistrarDecisionCommand request, CancellationToken ct)
    {
        var decision = new Decision(
            request.ProcesoId,
            request.ProveedorId,
            request.Accion,
            request.RazonDescarte);

        await _decisiones.GuardarAsync(decision, ct);
    }
}
