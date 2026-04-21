using Secop.Domain.Entities;

namespace Secop.Application.Interfaces;

public interface IProveedorRepository
{
    Task<Proveedor?> ObtenerPorIdAsync(Guid id, CancellationToken ct = default);
    Task<IEnumerable<Proveedor>> ObtenerTodosAsync(CancellationToken ct = default);

    /// <summary>
    /// Proveedores cuyo RUP vence dentro de los próximos <paramref name="diasAlerta"/> días.
    /// </summary>
    Task<IEnumerable<Proveedor>> ObtenerConRupPorVencerAsync(int diasAlerta = 30, CancellationToken ct = default);
    Task GuardarAsync(Proveedor proveedor, CancellationToken ct = default);
    Task ActualizarAsync(Proveedor proveedor, CancellationToken ct = default);
}
