using Secop.Domain.Entities;

namespace Secop.Application.Interfaces;

public interface IScoringService
{
    /// <summary>
    /// Evalúa relevancia, elegibilidad y accionabilidad. No calcula un puntaje total sintético.
    /// </summary>
    Task<Puntaje> CalcularAsync(Proveedor proveedor, Proceso proceso, float similitud);
}
