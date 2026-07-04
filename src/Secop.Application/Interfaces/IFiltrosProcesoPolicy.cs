namespace Secop.Application.Interfaces;

/// <summary>
/// Política configurable de descarte de procesos por régimen especial publicitario.
/// Implementada en Infrastructure leyendo configuración (Application nunca lee IConfiguration).
/// </summary>
public interface IFiltrosProcesoPolicy
{
    /// <summary>
    /// Si es true (valor por defecto), solo se descartan los procesos de régimen especial
    /// puramente publicitarios (sin "(con ofertas)"). Los procesos "con ofertas" nunca se descartan
    /// por este filtro.
    /// </summary>
    bool DescartarSoloPublicitario { get; }
}
