using FluentAssertions;
using Secop.Domain.Enums;
using Secop.Domain.Services;

namespace Secop.Domain.Tests.Services;

public class ElegibilidadProcesoTests
{
    [Theory]
    [InlineData(60_000_000, ModalidadContrato.LicitacionPublica, true)]
    [InlineData(59_999_999.99, ModalidadContrato.LicitacionPublica, false)]
    [InlineData(80_000_000, ModalidadContrato.ContratacionDirecta, true)]
    [InlineData(75_000_000, ModalidadContrato.SeleccionAbreviada, true)]
    [InlineData(110_000_000, ModalidadContrato.ConcursoMeritos, true)]
    [InlineData(0, ModalidadContrato.LicitacionPublica, false)]
    [InlineData(-1, ModalidadContrato.LicitacionPublica, false)]
    public void EsElegible_IgnoraModalidadTemporalmenteYAplicaLimiteInclusivo(
        decimal presupuestoCop,
        ModalidadContrato modalidad,
        bool esperado)
    {
        ElegibilidadProceso.EsElegible(modalidad, presupuestoCop).Should().Be(esperado);
    }

    [Fact]
    public void EsElegible_SinPresupuesto_Rechaza()
    {
        ElegibilidadProceso.EsElegible(ModalidadContrato.LicitacionPublica, null).Should().BeFalse();
    }
}
