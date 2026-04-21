using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Microsoft.EntityFrameworkCore.Storage.ValueConversion;
using Pgvector;
using Secop.Domain.Entities;

namespace Secop.Infrastructure.Persistence.Configurations;

public class ProveedorConfiguration : IEntityTypeConfiguration<Proveedor>
{
    public void Configure(EntityTypeBuilder<Proveedor> builder)
    {
        builder.ToTable("proveedores");

        builder.HasKey(p => p.Id);
        builder.Property(p => p.Id).HasColumnName("id");

        builder.Property(p => p.Nombre)              .HasColumnName("nombre").IsRequired();
        builder.Property(p => p.Nit)                 .HasColumnName("nit").IsRequired();
        builder.HasIndex(p => p.Nit).IsUnique();

        builder.Property(p => p.RupVigencia)         .HasColumnName("rup_vigencia");
        builder.Property(p => p.CapacidadFinanciera) .HasColumnName("capacidad_financiera").HasColumnType("decimal(18,2)");
        builder.Property(p => p.CodigosUnspsc)       .HasColumnName("codigos_unspsc").HasColumnType("text[]");
        builder.Property(p => p.ExperienciaDescripcion).HasColumnName("experiencia").HasColumnType("text[]");
        builder.Property(p => p.PalabrasClave)       .HasColumnName("palabras_clave").HasColumnType("text[]");
        builder.Property(p => p.TelegramChatId)      .HasColumnName("telegram_chat_id");
        builder.Property(p => p.CreadoEn)            .HasColumnName("creado_en");
        builder.Property(p => p.ActualizadoEn)       .HasColumnName("actualizado_en");

        // pgvector: vector(1536) — texto embebido del perfil del proveedor
        var vectorConverter = new ValueConverter<float[]?, Vector?>(
            v => v == null ? null : new Vector(v),
            v => v == null ? null : v.ToArray());

        builder.Property(p => p.Embedding)
            .HasColumnName("embedding")
            .HasColumnType("vector(1536)")
            .HasConversion(vectorConverter);
    }
}
