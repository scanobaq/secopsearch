using Microsoft.EntityFrameworkCore;
using Secop.Application.Interfaces;
using Secop.Domain.Entities;

namespace Secop.Infrastructure.Persistence.Repositories;

public class ProveedorRepository : IProveedorRepository
{
    private readonly AppDbContext _context;

    public ProveedorRepository(AppDbContext context) => _context = context;

    public Task<Proveedor?> ObtenerPorIdAsync(Guid id, CancellationToken ct) =>
        _context.Proveedores.FirstOrDefaultAsync(p => p.Id == id, ct);

    public async Task<IEnumerable<Proveedor>> ObtenerTodosAsync(CancellationToken ct) =>
        await _context.Proveedores.ToListAsync(ct);

    public async Task<IEnumerable<Proveedor>> ObtenerConRupPorVencerAsync(int diasAlerta = 30, CancellationToken ct = default)
    {
        var limite = DateTime.UtcNow.AddDays(diasAlerta);
        return await _context.Proveedores
            .Where(p => p.RupVigencia <= limite && p.RupVigencia > DateTime.UtcNow)
            .ToListAsync(ct);
    }

    public async Task GuardarAsync(Proveedor proveedor, CancellationToken ct)
    {
        var existe = await _context.Proveedores.AnyAsync(p => p.Id == proveedor.Id, ct);
        if (existe)
            _context.Proveedores.Update(proveedor);
        else
            await _context.Proveedores.AddAsync(proveedor, ct);

        await _context.SaveChangesAsync(ct);
    }

    public async Task ActualizarAsync(Proveedor proveedor, CancellationToken ct)
    {
        _context.Proveedores.Update(proveedor);
        await _context.SaveChangesAsync(ct);
    }
}
