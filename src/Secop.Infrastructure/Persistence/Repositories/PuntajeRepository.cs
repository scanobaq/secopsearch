using Microsoft.EntityFrameworkCore;
using Secop.Application.Interfaces;
using Secop.Domain.Entities;

namespace Secop.Infrastructure.Persistence.Repositories;

public class PuntajeRepository : IPuntajeRepository
{
    private readonly AppDbContext _context;

    public PuntajeRepository(AppDbContext context) => _context = context;

    public Task<Puntaje?> ObtenerAsync(string procesoId, Guid proveedorId, CancellationToken ct) =>
        _context.Puntajes
            .FirstOrDefaultAsync(p => p.ProcesoId == procesoId && p.ProveedorId == proveedorId, ct);

    public async Task<IEnumerable<Puntaje>> ObtenerPorProveedorAsync(Guid proveedorId, CancellationToken ct) =>
        await _context.Puntajes
            .Where(p => p.ProveedorId == proveedorId)
            .OrderByDescending(p => p.PuntajeTotal)
            .ToListAsync(ct);

    public async Task GuardarAsync(Puntaje puntaje, CancellationToken ct)
    {
        var existente = await ObtenerAsync(puntaje.ProcesoId, puntaje.ProveedorId, ct);
        if (existente is not null)
        {
            // Upsert: reemplazar con el nuevo puntaje calculado
            _context.Puntajes.Remove(existente);
            await _context.SaveChangesAsync(ct);
        }

        await _context.Puntajes.AddAsync(puntaje, ct);
        await _context.SaveChangesAsync(ct);
    }

    public async Task GuardarRangoAsync(IEnumerable<Puntaje> puntajes, CancellationToken ct)
    {
        await _context.Puntajes.AddRangeAsync(puntajes, ct);
        await _context.SaveChangesAsync(ct);
    }
}
