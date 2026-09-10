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
            relevanciaPorcentaje: 75f,
            elegibilidad: EstadoElegibilidad.Eligible,
            accionabilidad: EstadoAccionabilidad.Actionable,
            recomendacionAutomatica: RecomendacionAutomatica.Analyze,
            razones: [],
            calculadoEn: DateTime.UtcNow);

    [Fact]
    public void AgregarRazon_AgregaAlColeccion()
    {
        var puntaje = CrearPuntaje();

        puntaje.AgregarRazon("Razón de prueba");

        puntaje.Razones.Should().ContainSingle()
            .Which.Should().Be("Razón de prueba");
    }

    [Fact]
    public void AgregarRazon_NoAgregaDuplicadoCaseInsensitive()
    {
        var puntaje = CrearPuntaje();
        puntaje.AgregarRazon("Plazo muy corto");

        puntaje.AgregarRazon("plazo muy corto");

        puntaje.Razones.Should().ContainSingle();
    }

    [Fact]
    public void AgregarRazon_IgnoraWhitespace()
    {
        var puntaje = CrearPuntaje();

        puntaje.AgregarRazon("   ");

        puntaje.Razones.Should().BeEmpty();
    }
}
