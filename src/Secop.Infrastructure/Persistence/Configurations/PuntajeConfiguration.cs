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

        builder.Property(p => p.ProcesoId).HasColumnName("proceso_id").IsRequired();
        builder.Property(p => p.ProveedorId).HasColumnName("proveedor_id");
        builder.Property(p => p.RelevanciaPorcentaje).HasColumnName("relevancia_porcentaje");
        builder.Property(p => p.Elegibilidad).HasColumnName("elegibilidad").HasConversion<string>();
        builder.Property(p => p.Accionabilidad).HasColumnName("accionabilidad").HasConversion<string>();
        builder.Property(p => p.RecomendacionAutomatica)
            .HasColumnName("recomendacion_automatica")
            .HasConversion<string>();
        builder.Property(p => p.Razones).HasColumnName("razones").HasColumnType("text[]");

        // Columnas heredadas: se mantienen durante esta evolución para no renombrar la tabla.
        builder.Property<float>("PuntajeTotal").HasColumnName("puntaje_total");
        builder.Property<float>("PuntajeSimilitud").HasColumnName("puntaje_similitud");
        builder.Property<float>("PuntajeRequisitos").HasColumnName("puntaje_requisitos");
        builder.Property<float>("PuntajeTiempo").HasColumnName("puntaje_tiempo");
        builder.Property<float>("PuntajeCompetencia").HasColumnName("puntaje_competencia");
        builder.Property<float>("PuntajeEntidad").HasColumnName("puntaje_entidad");
        builder.Property<EtiquetaProceso>("Etiqueta").HasColumnName("etiqueta").HasConversion<string>();
        builder.PrimitiveCollection<List<string>>("Advertencias")
            .HasColumnName("advertencias")
            .HasColumnType("text[]");
        builder.Property<bool>("EsInhabilitado").HasColumnName("es_inhabilitado");
        builder.Property(p => p.CalculadoEn).HasColumnName("calculado_en");

        // Restricción única: un puntaje por proceso+proveedor (upsert en repo)
        builder.HasIndex(p => new { p.ProcesoId, p.ProveedorId }).IsUnique();
    }
}
