using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Secop.Domain.Entities;
using Secop.Domain.Enums;

namespace Secop.Infrastructure.Persistence.Configurations;

public class PuntajeConfiguration : IEntityTypeConfiguration<Puntaje>
{
    public void Configure(EntityTypeBuilder<Puntaje> builder)
    {
        builder.ToTable("puntajes");

        builder.HasKey(p => p.Id);
        builder.Property(p => p.Id).HasColumnName("id");

        builder.Property(p => p.ProcesoId)         .HasColumnName("proceso_id").IsRequired();
        builder.Property(p => p.ProveedorId)        .HasColumnName("proveedor_id");
        builder.Property(p => p.PuntajeTotal)       .HasColumnName("puntaje_total");
        builder.Property(p => p.PuntajeSimilitud)   .HasColumnName("puntaje_similitud");
        builder.Property(p => p.PuntajeRequisitos)  .HasColumnName("puntaje_requisitos");
        builder.Property(p => p.PuntajeTiempo)      .HasColumnName("puntaje_tiempo");
        builder.Property(p => p.PuntajeCompetencia) .HasColumnName("puntaje_competencia");
        builder.Property(p => p.PuntajeEntidad)     .HasColumnName("puntaje_entidad");
        builder.Property(p => p.Etiqueta)           .HasColumnName("etiqueta").HasConversion<string>();
        builder.Property(p => p.Advertencias)       .HasColumnName("advertencias").HasColumnType("text[]");
        builder.Property(p => p.EsInhabilitado)     .HasColumnName("es_inhabilitado");
        builder.Property(p => p.CalculadoEn)        .HasColumnName("calculado_en");

        // Restricción única: un puntaje por proceso+proveedor (upsert en repo)
        builder.HasIndex(p => new { p.ProcesoId, p.ProveedorId }).IsUnique();
    }
}
