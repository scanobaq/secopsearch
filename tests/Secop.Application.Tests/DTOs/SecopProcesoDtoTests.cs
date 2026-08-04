using System.Globalization;
using System.Text.Json;
using FluentAssertions;
using Secop.Application.DTOs;
using Secop.Domain.Enums;

namespace Secop.Application.Tests.DTOs;

public class SecopProcesoDtoTests
{
    [Theory]
    [InlineData("Presentación de oferta")]
    [InlineData("Fase de ofertas")]
    public void EstaAbiertoParaAplicar_FaseConocidaAceptada_PermaneceAplicable(string fase)
    {
        var dto = new SecopProcesoDto
        {
            EstadoApertura = "Abierto",
            Estado = "Publicado",
            Fase = fase
        };

        dto.EstaAbiertoParaAplicar().Should().BeTrue();
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("  no definido  ")]
    public void EstaAbiertoParaAplicar_SinFaseConResumenIndefinidoYFechaFutura_EsAplicable(string? estadoResumen)
    {
        var dto = CrearDtoParaFallback();
        dto.EstadoResumen = estadoResumen;

        dto.EstaAbiertoParaAplicar().Should().BeTrue();
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("fecha-invalida")]
    public void EstaAbiertoParaAplicar_FallbackSinFechaExplicitaValida_NoEsAplicable(string? fechaCierre)
    {
        var dto = CrearDtoParaFallback();
        dto.FechaCierre = fechaCierre;

        dto.EstaAbiertoParaAplicar().Should().BeFalse();
    }

    [Fact]
    public void EstaAbiertoParaAplicar_FallbackConFechaExpirada_NoEsAplicable()
    {
        var dto = CrearDtoParaFallback();
        dto.FechaCierre = DateTime.UtcNow.AddDays(-1).ToString("O");

        dto.EstaAbiertoParaAplicar().Should().BeFalse();
    }

    [Fact]
    public void EstaAbiertoParaAplicar_FallbackConInstanteExpiradoHoy_NoEsAplicable()
    {
        var dto = CrearDtoParaFallback();
        var inicioDelDiaUtc = new DateTimeOffset(DateTime.UtcNow.Date, TimeSpan.Zero);
        dto.FechaCierre = inicioDelDiaUtc.ToString("yyyy-MM-dd'T'HH:mm:ss", CultureInfo.InvariantCulture);

        dto.EstaAbiertoParaAplicar().Should().BeFalse();
    }

    [Fact]
    public void EstaAbiertoParaAplicar_FallbackConIsoZFraccionalFuturo_EsAplicable()
    {
        var dto = CrearDtoParaFallback();
        dto.FechaCierre = DateTimeOffset.UtcNow.AddHours(1)
            .ToString("yyyy-MM-dd'T'HH:mm:ss.fff'Z'", CultureInfo.InvariantCulture);

        dto.EstaAbiertoParaAplicar().Should().BeTrue();
    }

    [Fact]
    public void EstaAbiertoParaAplicar_FallbackConOffsetQueRepresentaInstanteFuturo_EsAplicable()
    {
        var dto = CrearDtoParaFallback();
        dto.FechaCierre = DateTimeOffset.UtcNow.AddHours(1)
            .ToOffset(TimeSpan.FromHours(-5))
            .ToString("yyyy-MM-dd'T'HH:mm:ss.fffffffzzz", CultureInfo.InvariantCulture);

        dto.EstaAbiertoParaAplicar().Should().BeTrue();
    }

    [Fact]
    public void EstaAbiertoParaAplicar_FallbackSinOffset_InterpretaInstanteComoUtc()
    {
        var dto = CrearDtoParaFallback();
        dto.FechaCierre = DateTimeOffset.UtcNow.AddHours(1)
            .ToString("yyyy-MM-dd'T'HH:mm:ss", CultureInfo.InvariantCulture);

        dto.EstaAbiertoParaAplicar().Should().BeTrue();
    }

    [Theory]
    [InlineData("07/08/2099 12:00:00")]
    [InlineData("31/12/2099 23:59:59")]
    public void EstaAbiertoParaAplicar_FallbackConFechaNoIsoAmbigua_NoEsAplicable(string fechaCierre)
    {
        var dto = CrearDtoParaFallback();
        dto.FechaCierre = fechaCierre;

        dto.EstaAbiertoParaAplicar().Should().BeFalse();
    }

    [Theory]
    [InlineData("Cerrado", "Publicado")]
    [InlineData("Abierto", "Borrador")]
    public void EstaAbiertoParaAplicar_FallbackSinEstadoAbiertoOPublicado_NoEsAplicable(
        string estadoApertura,
        string estado)
    {
        var dto = CrearDtoParaFallback();
        dto.EstadoApertura = estadoApertura;
        dto.Estado = estado;

        dto.EstaAbiertoParaAplicar().Should().BeFalse();
    }

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
    public void ObtenerCategoriasAdicionales_FormatoRealSinPunto_ParseaCorrectamente()
    {
        // El campo real del API nunca trae punto tras "V1" (confirmado en vivo:
        // "V172101500, V172103300"), a diferencia de codigo_principal_de_categoria
        // que sí lo trae ("V1.80111500").
        var dto = new SecopProcesoDto { CategoriasAdicionales = "V172101500, V172103300, V172141000" };

        dto.ObtenerCategoriasAdicionales().Should().BeEquivalentTo(["72101500", "72103300", "72141000"]);
    }

    [Fact]
    public void ObtenerCategoriasAdicionales_NuloOVacio_RetornaListaVacia()
    {
        var dto = new SecopProcesoDto { CategoriasAdicionales = null };

        dto.ObtenerCategoriasAdicionales().Should().BeEmpty();
    }

    [Fact]
    public void Deserializar_MapeaCodigoPrincipalDeCategoria()
    {
        var dto = JsonSerializer.Deserialize<SecopProcesoDto>(
            """{"codigo_principal_de_categoria":"V1.12345678"}""");

        dto!.CodigoPrincipalCategoria.Should().Be("V1.12345678");
    }

    private static SecopProcesoDto CrearDtoParaFallback() => new()
    {
        EstadoApertura = "Abierto",
        Estado = "Publicado",
        Fase = " ",
        EstadoResumen = "No Definido",
        FechaCierre = DateTime.UtcNow.AddDays(1).ToString("O")
    };
}
