using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Secop.Domain.Entities;

namespace Secop.Infrastructure.Persistence.Configurations;

public class AlertaConfiguration : IEntityTypeConfiguration<Alerta>
{
    public void Configure(EntityTypeBuilder<Alerta> builder)
    {
        builder.ToTable("alertas");

        builder.HasKey(a => a.Id);
        builder.Property(a => a.Id)               .HasColumnName("id");
        builder.Property(a => a.ProcesoId)        .HasColumnName("proceso_id");
        builder.Property(a => a.ProveedorId)      .HasColumnName("proveedor_id");
        builder.Property(a => a.Tipo)             .HasColumnName("tipo").HasConversion<string>();
        builder.Property(a => a.EnviadaEn)        .HasColumnName("enviada_en");
        builder.Property(a => a.TelegramMessageId).HasColumnName("telegram_message_id");
    }
}
