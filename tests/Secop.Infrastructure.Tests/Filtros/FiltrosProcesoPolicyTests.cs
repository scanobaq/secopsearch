using FluentAssertions;
using Microsoft.Extensions.Configuration;
using Secop.Infrastructure.Filtros;

namespace Secop.Infrastructure.Tests.Filtros;

public class FiltrosProcesoPolicyTests
{
    private static IConfiguration CrearConfiguracion(string? valor = null)
    {
        var datos = new Dictionary<string, string?>();
        if (valor is not null)
            datos["Filtros:DescartarSoloPublicitario"] = valor;

        return new ConfigurationBuilder().AddInMemoryCollection(datos).Build();
    }

    [Fact]
    public void DescartarSoloPublicitario_SinConfiguracion_DefaultTrue()
    {
        var policy = new FiltrosProcesoPolicy(CrearConfiguracion());

        policy.DescartarSoloPublicitario.Should().BeTrue();
    }

    [Fact]
    public void DescartarSoloPublicitario_ConfiguradoFalse_RespetaConfiguracion()
    {
        var policy = new FiltrosProcesoPolicy(CrearConfiguracion("false"));

        policy.DescartarSoloPublicitario.Should().BeFalse();
    }

    [Fact]
    public void DescartarSoloPublicitario_ConfiguradoTrue_RespetaConfiguracion()
    {
        var policy = new FiltrosProcesoPolicy(CrearConfiguracion("true"));

        policy.DescartarSoloPublicitario.Should().BeTrue();
    }
}
