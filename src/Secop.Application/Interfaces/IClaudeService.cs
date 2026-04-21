namespace Secop.Application.Interfaces;

public interface IClaudeService
{
    /// <summary>
    /// Descarga y analiza el pliego de condiciones (PDF) de un proceso SECOP.
    /// Devuelve un resumen estructurado con requisitos habilitantes, criterios de evaluación,
    /// documentos obligatorios y fechas clave.
    /// </summary>
    Task<string> AnalizarPliegoAsync(string urlProceso, CancellationToken ct = default);
}
