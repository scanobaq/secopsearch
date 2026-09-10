using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;
using Secop.Infrastructure.Persistence;

namespace Secop.Infrastructure.Tests.Persistence;

public class OpportunityEvaluationMigrationTests
{
    private static AppDbContext CrearContexto()
    {
        var options = new DbContextOptionsBuilder<AppDbContext>()
            .UseNpgsql(
                "Host=localhost;Port=5432;Database=dummy;Username=dummy;Password=dummy",
                npgsql => npgsql.UseVector())
            .Options;

        return new AppDbContext(options);
    }

    [Theory]
    [InlineData("relevancia_porcentaje")]
    [InlineData("elegibilidad")]
    [InlineData("accionabilidad")]
    [InlineData("recomendacion_automatica")]
    [InlineData("razones")]
    public void CreateScript_IncluyeDimensionPersistida(string columnaEsperada)
    {
        using var context = CrearContexto();

        context.Database.GenerateCreateScript().Should().Contain(columnaEsperada);
    }

    [Fact]
    public void MigrationScript_ConservaColumnasHeredadasYBackfillConservador()
    {
        using var context = CrearContexto();
        var migrator = context.GetService<IMigrator>();

        var script = migrator.GenerateScript(
            fromMigration: "20260707011102_RemoveEsConOfertas",
            toMigration: "20260827000000_AddOpportunityEvaluationDimensions");

        script.Should().Contain("puntaje_similitud * 100 / 35");
        script.Should().Contain("RequiresReview");
        script.Should().Contain("UnknownDate");
        script.Should().NotContain("DROP COLUMN puntaje_total");
    }

    [Fact]
    public void MigrationsHistory_IncluyeDimensionesDeEvaluacion()
    {
        using var context = CrearContexto();

        context.Database.GetMigrations().Should()
            .Contain("20260827000000_AddOpportunityEvaluationDimensions");
    }

    [Fact]
    public void ModelSnapshot_CoincideConModeloActual()
    {
        using var context = CrearContexto();

        context.Database.HasPendingModelChanges().Should().BeFalse();
    }
}
