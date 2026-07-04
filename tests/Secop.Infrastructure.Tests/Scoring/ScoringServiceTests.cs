using FluentAssertions;
using Microsoft.Extensions.Logging.Abstractions;
using Secop.Domain.Constants;
using Secop.Domain.Entities;
using Secop.Domain.Enums;
using Secop.Infrastructure.Scoring;

namespace Secop.Infrastructure.Tests.Scoring;

public class ScoringServiceTests
{
    private static ScoringService CrearServicio() => new(NullLogger<ScoringService>.Instance);

    private static Proveedor CrearProveedor() =>
        new(
            nombre: "Test S.A.",
            nit: "900100200",
            rupVigencia: DateTime.UtcNow.AddYears(1),
            capacidadFinanciera: 10_000_000m,
            codigosUnspsc: ["80101500"],
            experienciaDescripcion: ["Consultoría"]);

    private static Proceso CrearProceso(
        int? proveedoresInvitados = null,
        int? proveedoresQueManifestaron = null,
        int? respuestasAlProcedimiento = null,
        int? conteoRespuestasOfertas = null,
        int? proveedoresUnicosCon = null,
        string? tipoContrato = null) =>
        new(
            id: "PROC-001",
            titulo: "Título",
            objeto: "Objeto",
            presupuesto: 1_000_000m,
            fechaCierre: DateTime.UtcNow.AddDays(30),
            fechaPublicacion: DateTime.UtcNow.AddDays(-1),
            modalidad: ModalidadContrato.LicitacionPublica,
            estado: EstadoProceso.Activo,
            nombreEntidad: "Entidad",
            nitEntidad: "900999888",
            departamentoEntidad: "Bogotá",
            urlProceso: "https://secop.gov.co",
            tipoContrato: tipoContrato,
            proveedoresInvitados: proveedoresInvitados,
            proveedoresQueManifestaron: proveedoresQueManifestaron,
            respuestasAlProcedimiento: respuestasAlProcedimiento,
            conteoRespuestasOfertas: conteoRespuestasOfertas,
            proveedoresUnicosCon: proveedoresUnicosCon);

    // ─────────────────────────────────────────────────────────────────────────
    // SPEC-06: Competition component
    // ─────────────────────────────────────────────────────────────────────────

    [Fact]
    public async Task CalcularAsync_ContadoresNulos_CompetenciaEsNeutral6()
    {
        var proceso = CrearProceso();
        var puntaje = await CrearServicio().CalcularAsync(CrearProveedor(), proceso, 0.8f);

        puntaje.PuntajeCompetencia.Should().Be(6f);
    }

    [Fact]
    public async Task CalcularAsync_MenosCompetidores_MayorPuntajeQueMasCompetidores()
    {
        var procesoPocaCompetencia = CrearProceso(proveedoresUnicosCon: 1);
        var procesoAltaCompetencia = CrearProceso(proveedoresUnicosCon: 10);

        var puntajePoca = await CrearServicio().CalcularAsync(CrearProveedor(), procesoPocaCompetencia, 0.8f);
        var puntajeAlta = await CrearServicio().CalcularAsync(CrearProveedor(), procesoAltaCompetencia, 0.8f);

        puntajePoca.PuntajeCompetencia.Should().BeGreaterThan(puntajeAlta.PuntajeCompetencia);
    }

    [Fact]
    public async Task CalcularAsync_ConContadores_CompetenciaMayorQueCero()
    {
        var proceso = CrearProceso(proveedoresUnicosCon: 2);
        var puntaje = await CrearServicio().CalcularAsync(CrearProveedor(), proceso, 0.8f);

        puntaje.PuntajeCompetencia.Should().BeGreaterThan(0);
    }

    // ─────────────────────────────────────────────────────────────────────────
    // SPEC-07: ESAL discard reuses Puntaje.Inhabilitado
    // ─────────────────────────────────────────────────────────────────────────

    [Fact]
    public async Task CalcularAsync_ProcesoSoloEsal_RetornaInhabilitadoConAdvertencia()
    {
        var proceso = CrearProceso(tipoContrato: "Decreto 092 de 2017");
        var puntaje = await CrearServicio().CalcularAsync(CrearProveedor(), proceso, 0.9f);

        puntaje.EsInhabilitado.Should().BeTrue();
        puntaje.PuntajeTotal.Should().Be(0);
        puntaje.Etiqueta.Should().Be(EtiquetaProceso.Descartar);
        puntaje.Advertencias.Should().Contain(AdvertenciasPuntaje.SoloEsal);
    }

    [Fact]
    public async Task CalcularAsync_ProcesoNoEsal_NoQuedaInhabilitadoPorEsal()
    {
        var proceso = CrearProceso(tipoContrato: "Prestación de servicios");
        var puntaje = await CrearServicio().CalcularAsync(CrearProveedor(), proceso, 0.9f);

        puntaje.EsInhabilitado.Should().BeFalse();
        puntaje.Advertencias.Should().NotContain(AdvertenciasPuntaje.SoloEsal);
    }
}
