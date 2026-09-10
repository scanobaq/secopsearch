using FluentAssertions;
using Secop.Application.Services;
using Secop.Domain.Entities;
using Secop.Domain.Enums;

namespace Secop.Application.Tests.Services;

public class ConstructorTextoSemanticoTests
{
    [Fact]
    public void CrearParaProveedor_IncluyeSoloExperienciaYPalabrasClaveNormalizadas()
    {
        var proveedor = new Proveedor(
            nombre: "Logística y Eventos SAS",
            nit: "900123456",
            rupVigencia: DateTime.UtcNow.AddYears(1),
            capacidadFinanciera: 7_000_000_000m,
            codigosUnspsc: ["80141900", "90101600"],
            experienciaDescripcion:
            [
                "  Producción   de eventos corporativos ",
                "Operación\nlogística nacional",
                "producción de eventos corporativos"
            ],
            palabrasClave: [" logística ", "eventos   BTL", "LOGÍSTICA"]);

        var texto = ConstructorTextoSemantico.CrearParaProveedor(proveedor);

        texto.Should().Be(
            "Experiencia y capacidades: Producción de eventos corporativos | Operación logística nacional" +
            Environment.NewLine +
            "Palabras clave de búsqueda: logística | eventos BTL");
        texto.Should().NotContain(proveedor.Nombre);
        texto.Should().NotContain("7000000000");
        texto.Should().NotContainAny(proveedor.CodigosUnspsc.ToArray());
    }

    [Fact]
    public void CrearParaProveedor_SinCapacidadesTextuales_RetornaTextoVacio()
    {
        var proveedor = new Proveedor(
            nombre: "Proveedor vacío",
            nit: "900123456",
            rupVigencia: DateTime.UtcNow.AddYears(1),
            capacidadFinanciera: 1m,
            codigosUnspsc: ["80141900"],
            experienciaDescripcion: [" ", "\n"],
            palabrasClave: []);

        ConstructorTextoSemantico.CrearParaProveedor(proveedor).Should().BeEmpty();
    }

    [Fact]
    public void CrearParaProceso_IncluyeSoloTituloYObjetoNormalizados()
    {
        var proceso = new Proceso(
            id: "CO1.REQ.10810339",
            titulo: "  Consultoría   para diseño estructural ",
            objeto: "Estudios\ny diseños arquitectónicos para una sede pública",
            presupuesto: 174_901_043m,
            fechaCierre: DateTime.UtcNow.AddDays(30),
            fechaPublicacion: DateTime.UtcNow,
            modalidad: ModalidadContrato.LicitacionPublica,
            estado: EstadoProceso.Activo,
            nombreEntidad: "Secretaría de Infraestructura",
            nitEntidad: "899999999",
            departamentoEntidad: "Cundinamarca",
            urlProceso: "https://secop.gov.co/process");

        var texto = ConstructorTextoSemantico.CrearParaProceso(proceso);

        texto.Should().Be(
            "Consultoría para diseño estructural " +
            "Estudios y diseños arquitectónicos para una sede pública");
        texto.Should().NotContain(proceso.NombreEntidad);
        texto.Should().NotContain("174901043");
        texto.Should().NotContain(proceso.Id);
    }

    [Fact]
    public void CrearParaProceso_ConCamposVacios_RetornaTextoVacio()
    {
        var proceso = new Proceso(
            "PROC-EMPTY", " ", "\n", 0m,
            DateTime.UtcNow.AddDays(1), DateTime.UtcNow,
            ModalidadContrato.Otro, EstadoProceso.Activo,
            "Entidad", "900", "Bogotá", "https://secop.gov.co");

        ConstructorTextoSemantico.CrearParaProceso(proceso).Should().BeEmpty();
    }
}
