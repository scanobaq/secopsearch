using Secop.Application.Interfaces;
using Secop.Domain.Entities;

namespace Secop.Infrastructure.Persistence.Repositories;

public class DecisionRepository : IDecisionRepository
{
    private readonly AppDbContext _context;

    public DecisionRepository(AppDbContext context) => _context = context;

    public async Task GuardarAsync(Decision decision, CancellationToken ct)
    {
        await _context.Decisiones.AddAsync(decision, ct);
        await _context.SaveChangesAsync(ct);
    }
}
