using Secop.Application.DTOs;

namespace Secop.Application.Interfaces;

public interface ISecopApiClient
{
    Task<List<SecopProcesoDto>> ObtenerProcesosRecientesAsync(
        DateTime desde,
        DateTime? hasta = null,
        IEnumerable<string>? codigosUnspsc = null,
        IEnumerable<string>? codigosClase = null,
        CancellationToken ct = default);

    Task<List<SecopProcesoDto>> ObtenerProcesosPorPalabraClaveAsync(
        DateTime desde,
        string palabraClave,
        CancellationToken ct = default);

    Task<List<SecopProcesoDto>> ObtenerDesiertosSinAlertaAsync(CancellationToken ct = default);
    Task<List<SecopProcesoDto>> ObtenerProcesosConPaginacionAsync(int limit, int offset, CancellationToken ct = default);
}
