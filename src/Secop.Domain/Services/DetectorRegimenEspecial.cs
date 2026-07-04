using Secop.Domain.Constants;

namespace Secop.Domain.Services;

/// <summary>
/// Detecta si el nombre de una entidad sugiere régimen especial de contratación
/// (universidades públicas, ESE, ESP, EICE, entidades financieras estatales).
/// Uso exclusivo: ADVERTIR — nunca descartar automáticamente (SPEC-04).
/// </summary>
public static class DetectorRegimenEspecial
{
    public static bool EsPosibleRegimenEspecial(string? nombreEntidad)
    {
        if (string.IsNullOrWhiteSpace(nombreEntidad))
            return false;

        var nombre = nombreEntidad.ToUpperInvariant();

        foreach (var patron in PatronesRegimenEspecial.Patrones)
        {
            if (nombre.Contains(patron, StringComparison.OrdinalIgnoreCase))
                return true;
        }

        return false;
    }
}
