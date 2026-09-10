using Secop.Domain.Entities;

namespace Secop.Application.Services;

public static class ConstructorTextoSemantico
{
    public static string CrearParaProveedor(Proveedor proveedor)
    {
        ArgumentNullException.ThrowIfNull(proveedor);

        var secciones = new List<string>();
        AgregarSeccion(secciones, "Experiencia y capacidades", proveedor.ExperienciaDescripcion);
        AgregarSeccion(secciones, "Palabras clave de búsqueda", proveedor.PalabrasClave);

        return string.Join(Environment.NewLine, secciones);
    }

    public static string CrearParaProceso(Proceso proceso)
    {
        ArgumentNullException.ThrowIfNull(proceso);

        return string.Join(
            ' ',
            new[] { proceso.Titulo, proceso.Objeto }
                .Where(valor => !string.IsNullOrWhiteSpace(valor))
                .Select(NormalizarEspacios));
    }

    private static void AgregarSeccion(
        ICollection<string> secciones,
        string etiqueta,
        IEnumerable<string>? valores)
    {
        if (valores is null)
            return;

        var normalizados = valores
            .Where(valor => !string.IsNullOrWhiteSpace(valor))
            .Select(NormalizarEspacios)
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToList();

        if (normalizados.Count > 0)
            secciones.Add($"{etiqueta}: {string.Join(" | ", normalizados)}");
    }

    private static string NormalizarEspacios(string valor) =>
        string.Join(' ', valor.Split((char[]?)null, StringSplitOptions.RemoveEmptyEntries));
}
