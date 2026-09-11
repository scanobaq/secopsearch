using FluentAssertions;
using Secop.Domain.Constants;

namespace Secop.Domain.Tests.ValueObjects;

public class PoliticaEvaluacionTests
{
    [Theory]
    [InlineData(0.399999f, "80101599", "80101500", null, false)]
    [InlineData(0.40f, "80101599", "80101600", null, false)]
    [InlineData(0.40f, "80101599", "80101600", "80101500", true)]
    [InlineData(0.40f, "80101599", "80101500", null, true)]
    [InlineData(0.45f, "malformed", null, null, true)]
    public void Admitir_AplicaUmbralesYEvidenciaDeClase(
        float similitud,
        string codigoProveedor,
        string? codigoPrincipal,
        string? categoriaAdicional,
        bool esperado)
    {
        var admitido = PoliticaEvaluacion.Admitir(
            similitud,
            [codigoProveedor],
            codigoPrincipal,
            categoriaAdicional is null ? [] : [categoriaAdicional]);

        admitido.Should().Be(esperado);
    }

    [Theory]
    [InlineData("UNSPECIFIED", "80101500")]
    [InlineData("٨٠١٠١٥٩٩", "80101500")]
    [InlineData("80101599", "No definido")]
    public void Admitir_EvidenciaInvalidaNoReduceElUmbral(string codigoProveedor, string codigoProceso)
    {
        var admitido = PoliticaEvaluacion.Admitir(0.40f, [codigoProveedor], codigoProceso, []);

        admitido.Should().BeFalse();
    }
}
