using Secop.Domain.Entities;
using Secop.Domain.Constants;

namespace Secop.Application.Interfaces;

public interface IProcesoRepository
{
    Task<Proceso?> ObtenerPorIdAsync(string id, CancellationToken ct = default);
    Task<IEnumerable<Proceso>> ObtenerActivosAsync(CancellationToken ct = default);
    Task<IEnumerable<Proceso>> ObtenerDesiertosSinAlertaAsync(CancellationToken ct = default);
    Task<bool> ExisteAsync(string id, CancellationToken ct = default);
    Task GuardarAsync(Proceso proceso, CancellationToken ct = default);
    Task GuardarRangoAsync(IEnumerable<Proceso> procesos, CancellationToken ct = default);

    /// <summary>
    /// Búsqueda vectorial por similitud coseno usando pgvector.
    /// Solo devuelve procesos cuya similitud alcance o supere el umbral mínimo.
    /// </summary>
    Task<IEnumerable<(Proceso Proceso, float Similitud)>> BuscarPorSimilitudAsync(
        float[] embedding,
        float umbralMinimo = PoliticaEvaluacion.UmbralSimilitud,
        int limite = 50,
        CancellationToken ct = default);
}
