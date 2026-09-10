using Secop.Domain.Entities;

namespace Secop.Application.Interfaces;

public interface IPuntajeRepository
{
    Task<Puntaje?> ObtenerAsync(string procesoId, Guid proveedorId, CancellationToken ct = default);
    Task<IEnumerable<Puntaje>> ObtenerPorProveedorAsync(Guid proveedorId, CancellationToken ct = default);
    Task<IEnumerable<Puntaje>> ObtenerPorProcesoAsync(string procesoId, CancellationToken ct = default);
    Task GuardarAsync(Puntaje puntaje, CancellationToken ct = default);
    Task GuardarRangoAsync(IEnumerable<Puntaje> puntajes, CancellationToken ct = default);
}
