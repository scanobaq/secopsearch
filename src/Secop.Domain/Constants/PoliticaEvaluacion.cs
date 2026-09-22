using Secop.Domain.ValueObjects;

namespace Secop.Domain.Constants;

public static class PoliticaEvaluacion
{
    public const float UmbralSimilitud = 0.40f;
    public const float UmbralAdmisionDirecta = 0.45f;
    public const float UmbralRelevanciaPorcentaje = UmbralSimilitud * 100f;

    public static bool Admitir(
        float similitud,
        IEnumerable<string> codigosProveedor,
        string? codigoPrincipalProceso,
        IEnumerable<string> categoriasAdicionales)
    {
        if (similitud < UmbralSimilitud)
            return false;

        if (similitud >= UmbralAdmisionDirecta)
            return true;

        var codigosProceso = new[] { codigoPrincipalProceso }
            .Concat(categoriasAdicionales);
        return codigosProveedor
            .Select(CrearCodigoValido)
            .Where(codigo => codigo is not null)
            .Cast<CodigoUnspsc>()
            .Any(codigoProveedor => codigosProceso
                .Select(CrearCodigoValido)
                .Where(codigo => codigo is not null)
                .Cast<CodigoUnspsc>()
                .Any(codigoProceso => codigoProveedor.ComparteClaseCon(codigoProceso)));
    }

    private static CodigoUnspsc? CrearCodigoValido(string? valor) =>
        CodigoUnspsc.TryCreate(valor, out var codigo) ? codigo : null;
}
