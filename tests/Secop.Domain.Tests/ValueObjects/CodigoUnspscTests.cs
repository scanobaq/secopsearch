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
}
