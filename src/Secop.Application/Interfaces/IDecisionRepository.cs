using Secop.Domain.Entities;

namespace Secop.Application.Interfaces;

public interface IDecisionRepository
{
    Task GuardarAsync(Decision decision, CancellationToken ct = default);
}
