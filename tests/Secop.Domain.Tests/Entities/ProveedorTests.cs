using FluentAssertions;
using Secop.Domain.Entities;

namespace Secop.Domain.Tests.Entities;

public class ProveedorTests
{
    private static Proveedor CrearProveedor(List<string>? palabrasClave = null) =>
        new(
            nombre: "Empresa Test",
            nit: "900000001",
            rupVigencia: DateTime.UtcNow.AddYears(1),
            capacidadFinanciera: 1_000_000m,
            codigosUnspsc: ["80101500"],
            experienciaDescripcion: ["Consultoría de software"],
            palabrasClave: palabrasClave);

    [Fact]
    public void PalabrasClave_DefaultsToEmptyList()
    {
        var proveedor = CrearProveedor();

        proveedor.PalabrasClave.Should().NotBeNull().And.BeEmpty();
    }

    [Fact]
    public void ActualizarPalabrasClave_DeduplicaYTrimmea()
    {
        var proveedor = CrearProveedor();

        proveedor.ActualizarPalabrasClave(["  software  ", "Software", "CONSULTORÍA", "consultoría", ""]);

        proveedor.PalabrasClave.Should().HaveCount(2)
            .And.Contain("software")
            .And.Contain("CONSULTORÍA");
    }
}
