using FluentAssertions;
using Secop.Application.DTOs;
using Secop.Domain.Enums;

namespace Secop.Application.Tests.DTOs;

public class SecopProcesoDtoTests
{
    // ─────────────────────────────────────────────────────────────────────────
    // SPEC-01: Modalidad field remap
    // ─────────────────────────────────────────────────────────────────────────

    [Theory]
    [InlineData("Contratación Directa", ModalidadContrato.ContratacionDirecta)]
    [InlineData("Licitación Pública", ModalidadContrato.LicitacionPublica)]
    [InlineData("Selección Abreviada de Menor Cuantía", ModalidadContrato.SeleccionAbreviada)]
    [InlineData("Concurso de Méritos Abierto", ModalidadContrato.ConcursoMeritos)]
    [InlineData("Mínima Cuantía", ModalidadContrato.MinimaCuantia)]
    [InlineData("Acuerdo Marco de Precios", ModalidadContrato.AcuerdoMarcoPrecios)]
    [InlineData("Régimen Especial", ModalidadContrato.Otro)]
    public void ObtenerModalidad_UsaModalidadDeContratacion(string modalidad, ModalidadContrato esperado)
    {
        var dto = new SecopProcesoDto { Modalidad = modalidad };

        dto.ObtenerModalidad().Should().Be(esperado);
    }

    // ─────────────────────────────────────────────────────────────────────────
    // SPEC-02: Contract regime classification
    // ─────────────────────────────────────────────────────────────────────────

    [Fact]
    public void ObtenerClasificacion_RfiCuandoEsSolicitudDeInformacion()
    {
        var dto = new SecopProcesoDto { Modalidad = "Solicitud de información a los Proveedores" };

        dto.ObtenerClasificacion().Should().Be(ClasificacionRegimen.Rfi);
    }

    [Theory]
    [InlineData("Contratación Régimen Especial")]
    [InlineData("Régimen Especial (con ofertas)")]
    public void ObtenerClasificacion_RegimenEspecialCuandoModalidadLoIndica(string modalidad)
    {
        var dto = new SecopProcesoDto { Modalidad = modalidad };

        dto.ObtenerClasificacion().Should().Be(ClasificacionRegimen.RegimenEspecial);
    }

    [Fact]
    public void ObtenerClasificacion_Ley80PorDefecto()
    {
        var dto = new SecopProcesoDto { Modalidad = "Licitación Pública" };

        dto.ObtenerClasificacion().Should().Be(ClasificacionRegimen.Ley80);
    }

    // ─────────────────────────────────────────────────────────────────────────
    // SPEC-05: Real adjudication state
    // ─────────────────────────────────────────────────────────────────────────

    [Fact]
    public void ObtenerEstado_AdjudicadoCuandoAdjudicadoTrueConDatosCompletos()
    {
        var dto = new SecopProcesoDto
        {
            Adjudicado = "Si",
            NombreProveedorAdjudicado = "Proveedor S.A.",
            FechaAdjudicacion = "2026-05-01"
        };

        dto.ObtenerEstado().Should().Be(EstadoProceso.Adjudicado);
    }

    [Theory]
    [InlineData("Suspendido", EstadoProceso.Suspendido)]
    [InlineData("Cancelado", EstadoProceso.Cancelado)]
    [InlineData("Desierto", EstadoProceso.Desierto)]
    [InlineData("Cerrado", EstadoProceso.Cerrado)]
    [InlineData("Seleccionado", EstadoProceso.Seleccionado)]
    [InlineData("Activo", EstadoProceso.Activo)]
    public void ObtenerEstado_MapeaValoresRealesDeEstadoDelProcedimiento(string estado, EstadoProceso esperado)
    {
        var dto = new SecopProcesoDto { Estado = estado };

        dto.ObtenerEstado().Should().Be(esperado);
    }

    // ─────────────────────────────────────────────────────────────────────────
    // SPEC-07: ESAL detection helper
    // ─────────────────────────────────────────────────────────────────────────

    [Theory]
    [InlineData("Decreto 092 de 2017", true)]
    [InlineData("DECRETO 092 DE 2017", true)]
    [InlineData("Prestación de servicios", false)]
    [InlineData(null, false)]
    public void EsElegibleEsal_DetectaDecreto092(string? tipoContrato, bool esperado)
    {
        var dto = new SecopProcesoDto { TipoContrato = tipoContrato };

        dto.EsElegibleEsal().Should().Be(esperado);
    }

    // ─────────────────────────────────────────────────────────────────────────
    // SPEC-08: categorias_adicionales parsing
    // ─────────────────────────────────────────────────────────────────────────

    [Fact]
    public void ObtenerCategoriasAdicionales_ParseaCodigosSeparadosPorComaConPrefijoV1()
    {
        var dto = new SecopProcesoDto { CategoriasAdicionales = "V1.80101500,V1.43211503" };

        dto.ObtenerCategoriasAdicionales().Should().BeEquivalentTo(["80101500", "43211503"]);
    }

    [Fact]
    public void ObtenerCategoriasAdicionales_NuloOVacio_RetornaListaVacia()
    {
        var dto = new SecopProcesoDto { CategoriasAdicionales = null };

        dto.ObtenerCategoriasAdicionales().Should().BeEmpty();
    }
}
