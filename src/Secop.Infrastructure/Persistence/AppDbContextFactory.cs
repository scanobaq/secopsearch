using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Design;

namespace Secop.Infrastructure.Persistence;

/// <summary>
/// Permite a las herramientas de EF Core (dotnet ef migrations add / database update)
/// instanciar AppDbContext en tiempo de diseño sin necesitar un startup project configurado.
///
/// La connection string se lee de la variable de entorno SECOP_DB o del argumento --connection.
/// Para migraciones usar la conexión DIRECTA de Supabase (port 5432), no el pooler.
/// </summary>
public class AppDbContextFactory : IDesignTimeDbContextFactory<AppDbContext>
{
    public AppDbContext CreateDbContext(string[] args)
    {
        var connStr = Environment.GetEnvironmentVariable("SECOP_DB")
            ?? throw new InvalidOperationException(
                "Definir la variable de entorno SECOP_DB con la connection string directa de Supabase " +
                "(Host=db.[ref].supabase.co;Port=5432;...). NO usar el pooler para migraciones.");

        var options = new DbContextOptionsBuilder<AppDbContext>()
            .UseNpgsql(connStr, npgsql => npgsql.UseVector())
            .Options;

        return new AppDbContext(options);
    }
}
