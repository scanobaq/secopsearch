using FluentAssertions;
using Secop.Domain.ValueObjects;

namespace Secop.Domain.Tests.ValueObjects;

public class CodigoUnspscTests
{
    [Fact]
    public void CodigoClase_ReturnsPrimerosSeisDígitos()
    {
        var codigo = new CodigoUnspsc("80101500");

        codigo.CodigoClase.Should().Be("801015");
    }

    [Fact]
    public void CodigoClase_Invariant_EqualsSegmentoFamiliaClase()
    {
        var codigo = new CodigoUnspsc("43211503");

        codigo.CodigoClase.Should().Be(codigo.Segmento + codigo.Familia + codigo.Clase);
    }

    [Theory]
    [InlineData("80101500")]
    [InlineData(" 80101500 ")]
    public void TryCreate_CodigoAsciiDeOchoDigitos_CreaCodigo(string valor)
    {
        var creado = CodigoUnspsc.TryCreate(valor, out var codigo);

        creado.Should().BeTrue();
        codigo!.Valor.Should().Be("80101500");
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("UNSPECIFIED")]
    [InlineData("No definido")]
    [InlineData("8010150")]
    [InlineData("801015000")]
    [InlineData("٨٠١٠١٥٠٠")]
    public void TryCreate_ValorNuloSentinelaOMalformado_NoCreaCodigo(string? valor)
    {
        var creado = CodigoUnspsc.TryCreate(valor, out var codigo);

        creado.Should().BeFalse();
        codigo.Should().BeNull();
    }

    [Fact]
    public void ComparteClaseCon_CodigosConLosMismosPrimerosSeisDigitos_ReturnsTrue()
    {
        var codigo = new CodigoUnspsc("80101500");
        var otro = new CodigoUnspsc("80101599");

        codigo.ComparteClaseCon(otro).Should().BeTrue();
        codigo.ComparteClaseCon(new CodigoUnspsc("80101600")).Should().BeFalse();
    }
}
