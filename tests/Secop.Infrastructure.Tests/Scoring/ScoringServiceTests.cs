using FluentAssertions;
using Microsoft.Extensions.Logging.Abstractions;
using Secop.Domain.Constants;
using Secop.Domain.Entities;
using Secop.Domain.Enums;
using Secop.Infrastructure.Scoring;

namespace Secop.Infrastructure.Tests.Scoring;

public class ScoringServiceTests
{
    private static readonly DateTimeOffset Ahora =
        new(2026, 8, 24, 12, 0, 0, TimeSpan.Zero);

    private static ScoringService CrearServicio() =>
        new(new TiempoFijo(Ahora), NullLogger<ScoringService>.Instance);

    private static Proveedor CrearProveedor(
        DateTime? rupVigencia = null,
        decimal capacidadFinanciera = 1_000_000m) =>
        new(
            nombre: "Test S.A.",
            nit: "900100200",
            rupVigencia: rupVigencia ?? Ahora.UtcDateTime.AddYears(1),
            capacidadFinanciera: capacidadFinanciera,
            codigosUnspsc: ["80101500"],
            experienciaDescripcion: ["Consultoría"]);

    private static Proceso CrearProceso(DateTime? fechaCierre = null, string? tipoContrato = null) =>
        new(
            id: "PROC-001",
            titulo: "Título",
            objeto: "Objeto",
            presupuesto: 1_000_000m,
            fechaCierre: fechaCierre ?? Ahora.UtcDateTime.AddDays(30),
            fechaPublicacion: Ahora.UtcDateTime.AddDays(-1),
            modalidad: ModalidadContrato.LicitacionPublica,
            estado: EstadoProceso.Activo,
            nombreEntidad: "Entidad",
            nitEntidad: "900999888",
            departamentoEntidad: "Bogotá",
            urlProceso: "https://secop.gov.co",
            tipoContrato: tipoContrato);

    [Theory]
    [InlineData(0.50f, 50f)]
    [InlineData(0.7342f, 73.42f)]
    public async Task CalcularAsync_ConvierteSimilitudARelevanciaPorcentual(
        float similitud,
        float porcentajeEsperado)
    {
        var evaluacion = await CrearServicio().CalcularAsync(
            CrearProveedor(), CrearProceso(), similitud);

        evaluacion.RelevanciaPorcentaje.Should().BeApproximately(porcentajeEsperado, 0.001f);
    }

    [Fact]
    public async Task CalcularAsync_RupVencidoDuranteExperimentacion_NoBloqueaElegibilidad()
    {
        var evaluacion = await CrearServicio().CalcularAsync(
            CrearProveedor(rupVigencia: Ahora.UtcDateTime.AddDays(-1)), CrearProceso(), 0.8f);

        evaluacion.Elegibilidad.Should().Be(EstadoElegibilidad.Eligible);
        evaluacion.Razones.Should().NotContain(RazonesEvaluacion.RupVencido);
        evaluacion.RecomendacionAutomatica.Should().Be(RecomendacionAutomatica.Analyze);
    }

    [Fact]
    public async Task CalcularAsync_ProcesoSoloEsal_EsIneligible()
    {
        var evaluacion = await CrearServicio().CalcularAsync(
            CrearProveedor(
                rupVigencia: Ahora.UtcDateTime.AddDays(-1),
                capacidadFinanciera: 1m),
            CrearProceso(tipoContrato: "Decreto 092 de 2017"),
            0.8f);

        evaluacion.Elegibilidad.Should().Be(EstadoElegibilidad.Ineligible);
        evaluacion.Razones.Should().Contain(RazonesEvaluacion.SoloEsal);
        evaluacion.Razones.Should().NotContain(RazonesEvaluacion.CapacidadInsuficiente);
    }

    [Fact]
    public async Task CalcularAsync_CapacidadInsuficiente_RequiereRevision()
    {
        var evaluacion = await CrearServicio().CalcularAsync(
            CrearProveedor(
                rupVigencia: Ahora.UtcDateTime.AddDays(-1),
                capacidadFinanciera: 149_999m),
            CrearProceso(),
            0.8f);

        evaluacion.Elegibilidad.Should().Be(EstadoElegibilidad.RequiresReview);
        evaluacion.Elegibilidad.Should().NotBe(EstadoElegibilidad.Ineligible);
        evaluacion.Razones.Should().Contain(RazonesEvaluacion.CapacidadInsuficiente);
    }

    [Fact]
    public async Task CalcularAsync_CapacidadSuficiente_EsEligible()
    {
        var evaluacion = await CrearServicio().CalcularAsync(
            CrearProveedor(capacidadFinanciera: 150_000m), CrearProceso(), 0.8f);

        evaluacion.Elegibilidad.Should().Be(EstadoElegibilidad.Eligible);
    }

    [Theory]
    [MemberData(nameof(CasosAccionabilidad))]
    public async Task CalcularAsync_ClasificaLimitesDeAccionabilidad(
        DateTime fechaCierre,
        EstadoAccionabilidad esperado)
    {
        var evaluacion = await CrearServicio().CalcularAsync(
            CrearProveedor(), CrearProceso(fechaCierre), 0.8f);

        evaluacion.Accionabilidad.Should().Be(esperado);
    }

    public static TheoryData<DateTime, EstadoAccionabilidad> CasosAccionabilidad => new()
    {
        { Ahora.UtcDateTime.AddHours(1), EstadoAccionabilidad.Urgent },
        { new DateTime(2026, 8, 31, 18, 0, 0, DateTimeKind.Utc), EstadoAccionabilidad.Urgent },
        { new DateTime(2026, 9, 1, 18, 0, 0, DateTimeKind.Utc), EstadoAccionabilidad.Actionable },
        { Ahora.UtcDateTime.AddMinutes(-1), EstadoAccionabilidad.InsufficientTime },
        { DateTime.MinValue, EstadoAccionabilidad.UnknownDate },
        { DateTime.MaxValue, EstadoAccionabilidad.UnknownDate }
    };

    [Fact]
    public async Task CalcularAsync_NuncaProduceProposeAutomatico()
    {
        var evaluacion = await CrearServicio().CalcularAsync(
            CrearProveedor(), CrearProceso(), 1f);

        evaluacion.RecomendacionAutomatica.Should().Be(RecomendacionAutomatica.Analyze);
        Enum.GetNames<RecomendacionAutomatica>().Should().Equal("Analyze");
    }

    private sealed class TiempoFijo(DateTimeOffset ahora) : TimeProvider
    {
        public override DateTimeOffset GetUtcNow() => ahora;
    }
}
