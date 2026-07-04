using FluentAssertions;
using Secop.Domain.Entities;
using Secop.Domain.Enums;

namespace Secop.Domain.Tests.Entities;

public class ProcesoTests
{
    private static Proceso CrearProceso(
        ClasificacionRegimen clasificacion = ClasificacionRegimen.Ley80,
        bool esConOfertas = false,
        string? tipoContrato = null,
        string? adjudicadoA = null,
        decimal? valorAdjudicacion = null,
        DateTime? fechaAdjudicacion = null,
        List<string>? categoriasAdicionales = null,
        int? proveedoresInvitados = null,
        int? proveedoresQueManifestaron = null,
        int? respuestasAlProcedimiento = null,
        int? conteoRespuestasOfertas = null,
        int? proveedoresUnicosCon = null) =>
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
            clasificacion: clasificacion,
            esConOfertas: esConOfertas,
            tipoContrato: tipoContrato,
            adjudicadoA: adjudicadoA,
            valorAdjudicacion: valorAdjudicacion,
            fechaAdjudicacion: fechaAdjudicacion,
            categoriasAdicionales: categoriasAdicionales,
            proveedoresInvitados: proveedoresInvitados,
            proveedoresQueManifestaron: proveedoresQueManifestaron,
            respuestasAlProcedimiento: respuestasAlProcedimiento,
            conteoRespuestasOfertas: conteoRespuestasOfertas,
            proveedoresUnicosCon: proveedoresUnicosCon);

    [Fact]
    public void Constructor_AsignaCamposNuevos()
    {
        var fecha = DateTime.UtcNow;
        var proceso = CrearProceso(
            clasificacion: ClasificacionRegimen.RegimenEspecial,
            esConOfertas: true,
            tipoContrato: "Prestación de servicios",
            adjudicadoA: "Proveedor S.A.",
            valorAdjudicacion: 500_000m,
            fechaAdjudicacion: fecha,
            categoriasAdicionales: ["80101500", "43211503"],
            proveedoresInvitados: 5,
            proveedoresQueManifestaron: 3,
            respuestasAlProcedimiento: 2,
            conteoRespuestasOfertas: 2,
            proveedoresUnicosCon: 2);

        proceso.Clasificacion.Should().Be(ClasificacionRegimen.RegimenEspecial);
        proceso.EsConOfertas.Should().BeTrue();
        proceso.TipoContrato.Should().Be("Prestación de servicios");
        proceso.AdjudicadoA.Should().Be("Proveedor S.A.");
        proceso.ValorAdjudicacion.Should().Be(500_000m);
        proceso.FechaAdjudicacion.Should().Be(fecha);
        proceso.CategoriasAdicionales.Should().BeEquivalentTo(["80101500", "43211503"]);
        proceso.ProveedoresInvitados.Should().Be(5);
        proceso.ProveedoresQueManifestaron.Should().Be(3);
        proceso.RespuestasAlProcedimiento.Should().Be(2);
        proceso.ConteoRespuestasOfertas.Should().Be(2);
        proceso.ProveedoresUnicosCon.Should().Be(2);
    }

    [Fact]
    public void Constructor_SinCategoriasAdicionales_DefaultsToEmptyList()
    {
        var proceso = CrearProceso(categoriasAdicionales: null);

        proceso.CategoriasAdicionales.Should().NotBeNull().And.BeEmpty();
    }

    [Theory]
    [InlineData("Decreto 092 de 2017")]
    [InlineData("DECRETO 092 DE 2017")]
    public void EsSoloEsal_TipoContratoDecreto092_ReturnsTrue(string tipoContrato)
    {
        var proceso = CrearProceso(tipoContrato: tipoContrato);

        proceso.EsSoloEsal().Should().BeTrue();
    }

    [Theory]
    [InlineData(null)]
    [InlineData("Prestación de servicios")]
    [InlineData("Obra")]
    public void EsSoloEsal_OtroTipoContrato_ReturnsFalse(string? tipoContrato)
    {
        var proceso = CrearProceso(tipoContrato: tipoContrato);

        proceso.EsSoloEsal().Should().BeFalse();
    }
}
