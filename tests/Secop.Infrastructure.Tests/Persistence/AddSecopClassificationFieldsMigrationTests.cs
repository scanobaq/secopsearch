using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using Secop.Infrastructure.Persistence;

namespace Secop.Infrastructure.Tests.Persistence;

/// <summary>
/// Verifica que la migración AddSecopClassificationFields agregue las columnas esperadas
/// al esquema, sin requerir una base de datos Postgres real: usamos
/// <see cref="RelationalDatabaseFacadeExtensions.GenerateCreateScript"/>, que solo introspecta
/// el modelo compilado (incluye todas las migraciones aplicadas) para generar el DDL,
/// sin abrir una conexión.
/// </summary>
public class AddSecopClassificationFieldsMigrationTests
{
    private static AppDbContext CrearContexto()
    {
        var options = new DbContextOptionsBuilder<AppDbContext>()
            .UseNpgsql("Host=localhost;Port=5432;Database=dummy;Username=dummy;Password=dummy",
                npgsql => npgsql.UseVector())
            .Options;

        return new AppDbContext(options);
    }

    [Theory]
    [InlineData("clasificacion")]
    [InlineData("es_con_ofertas")]
    [InlineData("tipo_contrato")]
    [InlineData("adjudicado_a")]
    [InlineData("valor_adjudicacion")]
    [InlineData("fecha_adjudicacion")]
    [InlineData("categorias_adicionales")]
    [InlineData("proveedores_invitados")]
    [InlineData("proveedores_que_manifestaron")]
    [InlineData("respuestas_al_procedimiento")]
    [InlineData("conteo_respuestas_ofertas")]
    [InlineData("proveedores_unicos_con")]
    public void CreateScript_IncludesNewColumn(string columnaEsperada)
    {
        using var context = CrearContexto();

        var script = context.Database.GenerateCreateScript();

        script.Should().Contain(columnaEsperada);
    }

    [Fact]
    public void MigrationsHistory_IncludesAddSecopClassificationFields()
    {
        using var context = CrearContexto();

        var migraciones = context.Database.GetMigrations();

        migraciones.Should().Contain(m => m.Contains("AddSecopClassificationFields"));
    }
}
