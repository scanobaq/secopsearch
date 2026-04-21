using Microsoft.EntityFrameworkCore;
using Secop.Domain.Entities;
using Secop.Infrastructure.Persistence.Configurations;

namespace Secop.Infrastructure.Persistence;

public class AppDbContext : DbContext
{
    public DbSet<Proceso>   Procesos   { get; set; }
    public DbSet<Proveedor> Proveedores { get; set; }
    public DbSet<Puntaje>   Puntajes   { get; set; }
    public DbSet<Decision>  Decisiones { get; set; }
    public DbSet<Alerta>    Alertas    { get; set; }

    public AppDbContext(DbContextOptions<AppDbContext> options) : base(options) { }

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        modelBuilder.HasPostgresExtension("vector");

        modelBuilder.ApplyConfiguration(new ProcesoConfiguration());
        modelBuilder.ApplyConfiguration(new ProveedorConfiguration());
        modelBuilder.ApplyConfiguration(new PuntajeConfiguration());
        modelBuilder.ApplyConfiguration(new DecisionConfiguration());
        modelBuilder.ApplyConfiguration(new AlertaConfiguration());
    }
}
