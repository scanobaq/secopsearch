using FluentAssertions;
using System.Reflection;
using Secop.Domain.Entities;
using Secop.Domain.Enums;

namespace Secop.Domain.Tests.Entities;

public class ProcesoTests
{
    private static Proceso CrearProceso(
        ClasificacionRegimen clasificacion = ClasificacionRegimen.Ley80,
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

    [Fact]
    public void Constructor_CodigoPrincipalOpcional_ConservaCompatibilidadPosicional()
    {
        var procesoExistente = new Proceso(
            "PROC-LEGACY", "Título", "Objeto", 1m, DateTime.UtcNow, DateTime.UtcNow,
            ModalidadContrato.LicitacionPublica, EstadoProceso.Activo, "Entidad", "900", "Bogotá", "url");
        var procesoConCodigo = new Proceso(
            "PROC-PRIMARY", "Título", "Objeto", 1m, DateTime.UtcNow, DateTime.UtcNow,
            ModalidadContrato.LicitacionPublica, EstadoProceso.Activo, "Entidad", "900", "Bogotá", "url",
            codigoPrincipalCategoria: "80101500");

        procesoExistente.CodigoPrincipalCategoria.Should().BeNull();
        procesoConCodigo.CodigoPrincipalCategoria.Should().Be("80101500");
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

    [Fact]
    public void EsSoloEsal_DespuesDeHidratacionEF_ReturnsTrue()
    {
        var constructor = typeof(Proceso).GetConstructor(
            BindingFlags.Instance | BindingFlags.NonPublic,
            binder: null,
            Type.EmptyTypes,
            modifiers: null);
        var proceso = (Proceso)constructor!.Invoke(null);
        typeof(Proceso).GetProperty(nameof(Proceso.TipoContrato))!
            .SetValue(proceso, "Decreto 092 de 2017");

        proceso.EsSoloEsal().Should().BeTrue();
    }
}
