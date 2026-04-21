using Secop.Domain.Entities;

namespace Secop.Application.Interfaces;

public interface IScoringService
{
    Task<Puntaje> CalcularAsync(Proveedor proveedor, Proceso proceso, float similitud);
}
