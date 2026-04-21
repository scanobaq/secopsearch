using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Secop.Domain.Entities;

namespace Secop.Infrastructure.Persistence.Configurations;

public class DecisionConfiguration : IEntityTypeConfiguration<Decision>
{
    public void Configure(EntityTypeBuilder<Decision> builder)
    {
        builder.ToTable("decisiones");

        builder.HasKey(d => d.Id);
        builder.Property(d => d.Id)            .HasColumnName("id");
        builder.Property(d => d.ProcesoId)     .HasColumnName("proceso_id").IsRequired();
        builder.Property(d => d.ProveedorId)   .HasColumnName("proveedor_id");
        builder.Property(d => d.Accion)        .HasColumnName("accion").HasConversion<string>();
        builder.Property(d => d.RazonDescarte) .HasColumnName("razon_descarte");
        builder.Property(d => d.Resultado)     .HasColumnName("resultado");
        builder.Property(d => d.RegistradoEn)  .HasColumnName("registrado_en");
    }
}
