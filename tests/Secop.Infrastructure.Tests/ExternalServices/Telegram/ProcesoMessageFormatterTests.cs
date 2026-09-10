using FluentAssertions;
using Secop.Domain.Entities;
using Secop.Domain.Enums;
using Secop.Infrastructure.ExternalServices.Telegram;

namespace Secop.Infrastructure.Tests.ExternalServices.Telegram;

public class ProcesoMessageFormatterTests
{
    private static Proceso CrearProceso(ModalidadContrato modalidad, ClasificacionRegimen clasificacion) =>
        new(
            id: "PROC-001",
            titulo: "Título",
            objeto: "Objeto",
            presupuesto: 1_000_000m,
            fechaCierre: DateTime.UtcNow.AddDays(30),
            fechaPublicacion: DateTime.UtcNow.AddDays(-1),
            modalidad: modalidad,
            estado: EstadoProceso.Activo,
            nombreEntidad: "Entidad",
            nitEntidad: "900999888",
            departamentoEntidad: "Bogotá",
            urlProceso: "https://secop.gov.co",
            clasificacion: clasificacion);

    private static Puntaje CrearPuntaje() =>
        new(
            "PROC-001",
            Guid.NewGuid(),
            80f,
            EstadoElegibilidad.RequiresReview,
            EstadoAccionabilidad.UnknownDate,
            RecomendacionAutomatica.Analyze,
            ["Fecha pendiente de confirmación"],
            DateTime.UtcNow);

    private static Proveedor CrearProveedor() =>
        new("Empresa SAS", "900111222", DateTime.UtcNow.AddYears(1), 1_000_000m, [], []);

    [Theory]
    [InlineData(ModalidadContrato.Otro, ClasificacionRegimen.Rfi, "Solicitud de información")]
    [InlineData(ModalidadContrato.Otro, ClasificacionRegimen.RegimenEspecial, "Régimen especial")]
    [InlineData(ModalidadContrato.LicitacionPublica, ClasificacionRegimen.Ley80, "Licitación pública")]
    [InlineData(ModalidadContrato.SeleccionAbreviada, ClasificacionRegimen.Ley80, "Selección abreviada")]
    [InlineData(ModalidadContrato.ConcursoMeritos, ClasificacionRegimen.Ley80, "Concurso de méritos")]
    [InlineData(ModalidadContrato.ContratacionDirecta, ClasificacionRegimen.Ley80, "Contratación directa")]
    [InlineData(ModalidadContrato.MinimaCuantia, ClasificacionRegimen.Ley80, "Mínima cuantía")]
    [InlineData(ModalidadContrato.AcuerdoMarcoPrecios, ClasificacionRegimen.Ley80, "Acuerdo marco de precios")]
    public void Formatear_MuestraNombreRealDeModalidad_NuncaOtro(
        ModalidadContrato modalidad, ClasificacionRegimen clasificacion, string nombreEsperado)
    {
        var proceso = CrearProceso(modalidad, clasificacion);

        var mensaje = ProcesoMessageFormatter.Formatear(CrearPuntaje(), proceso, CrearProveedor());

        mensaje.Should().Contain($"🏷 Modalidad: {nombreEsperado}");
        mensaje.Should().NotContain("Modalidad: Otro");
    }

    [Fact]
    public void Formatear_MuestraDimensionesSinPuntajeTotal()
    {
        var mensaje = ProcesoMessageFormatter.Formatear(
            CrearPuntaje(),
            CrearProceso(ModalidadContrato.LicitacionPublica, ClasificacionRegimen.Ley80),
            CrearProveedor());

        mensaje.Should().Contain("Relevancia: <b>80,0%</b>");
        mensaje.Should().Contain("Elegibilidad: <b>Requiere revisión</b>");
        mensaje.Should().Contain("Accionabilidad: <b>Fecha desconocida</b>");
        mensaje.Should().Contain("ANALIZAR");
        mensaje.Should().NotContain("Puntaje:");
        mensaje.Should().NotContain("PROPONER");
    }

    [Fact]
    public void Formatear_AdvierteSobreManifestacionDeInteres_YConservaEnlaceDirecto()
    {
        var mensaje = ProcesoMessageFormatter.Formatear(
            CrearPuntaje(),
            CrearProceso(ModalidadContrato.LicitacionPublica, ClasificacionRegimen.Ley80),
            CrearProveedor());

        mensaje.Should().Contain(
            "⚠️ Antes de preparar o presentar la oferta, verifica en SECOP II si existe un plazo previo para manifestación de interés.");
        mensaje.Should().Contain(
            "🔗 <a href=\"https://secop.gov.co\">Ver proceso en SECOP II</a>");
    }
}
