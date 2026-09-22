using FluentAssertions;
using Secop.Domain.Services;

namespace Secop.Domain.Tests.Services;

public class DetectorRegimenEspecialTests
{
    [Theory]
    [InlineData("UNIVERSIDAD NACIONAL DE COLOMBIA")]
    [InlineData("E.S.E HOSPITAL SAN JUAN DE DIOS")]
    [InlineData("ESE HOSPITAL DEPARTAMENTAL")]
    [InlineData("EMPRESA DE SERVICIOS PUBLICOS E.S.P DE NARIÑO")]
    [InlineData("EICE ACUEDUCTO METROPOLITANO")]
    [InlineData("FIDUCIARIA LA PREVISORA")]
    [InlineData("FINDETER")]
    public void EsPosibleRegimenEspecial_MatchesKnownPatterns(string nombreEntidad)
    {
        DetectorRegimenEspecial.EsPosibleRegimenEspecial(nombreEntidad).Should().BeTrue();
    }

    [Theory]
    [InlineData("ALCALDIA MUNICIPAL DE PASTO")]
    [InlineData("GOBERNACION DE NARIÑO")]
    [InlineData("MINISTERIO DE SALUD")]
    public void EsPosibleRegimenEspecial_DoesNotMatchOrdinaryEntities(string nombreEntidad)
    {
        DetectorRegimenEspecial.EsPosibleRegimenEspecial(nombreEntidad).Should().BeFalse();
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    public void EsPosibleRegimenEspecial_NullOrWhitespace_ReturnsFalse(string? nombreEntidad)
    {
        DetectorRegimenEspecial.EsPosibleRegimenEspecial(nombreEntidad).Should().BeFalse();
    }
}
