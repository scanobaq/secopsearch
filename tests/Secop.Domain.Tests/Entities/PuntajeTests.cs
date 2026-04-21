using FluentAssertions;
using Secop.Domain.Entities;
using Secop.Domain.Enums;

namespace Secop.Domain.Tests.Entities;

public class PuntajeTests
{
    private static Puntaje CrearPuntaje() =>
        new(
            procesoId: "PROC-001",
            proveedorId: Guid.NewGuid(),
            puntajeTotal: 75f,
            puntajeSimilitud: 28f,
            puntajeRequisitos: 20f,
            puntajeTiempo: 15f,
            puntajeCompetencia: 8f,
            puntajeEntidad: 4f,
            etiqueta: EtiquetaProceso.Proponer,
            advertencias: []);

    [Fact]
    public void AgregarAdvertencia_AgregaAlColeccion()
    {
        var puntaje = CrearPuntaje();

        puntaje.AgregarAdvertencia("Advertencia de prueba");

        puntaje.Advertencias.Should().ContainSingle()
            .Which.Should().Be("Advertencia de prueba");
    }

    [Fact]
    public void AgregarAdvertencia_NoAgregaDuplicadoCaseInsensitive()
    {
        var puntaje = CrearPuntaje();
        puntaje.AgregarAdvertencia("Plazo muy corto");

        puntaje.AgregarAdvertencia("plazo muy corto");

        puntaje.Advertencias.Should().ContainSingle();
    }

    [Fact]
    public void AgregarAdvertencia_IgnoraWhitespace()
    {
        var puntaje = CrearPuntaje();

        puntaje.AgregarAdvertencia("   ");

        puntaje.Advertencias.Should().BeEmpty();
    }
}
