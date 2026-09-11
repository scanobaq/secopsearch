using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Microsoft.EntityFrameworkCore.Storage.ValueConversion;
using Pgvector;
using Secop.Domain.Entities;

namespace Secop.Infrastructure.Persistence.Configurations;

public class ProcesoConfiguration : IEntityTypeConfiguration<Proceso>
{
    public void Configure(EntityTypeBuilder<Proceso> builder)
    {
        builder.ToTable("procesos");

        builder.HasKey(p => p.Id);
        builder.Property(p => p.Id).HasColumnName("id");

        builder.Property(p => p.Titulo)              .HasColumnName("titulo").IsRequired();
        builder.Property(p => p.Objeto)              .HasColumnName("objeto");
        builder.Property(p => p.Presupuesto)         .HasColumnName("presupuesto").HasColumnType("decimal(18,2)");
        builder.Property(p => p.FechaCierre)         .HasColumnName("fecha_cierre");
        builder.Property(p => p.FechaPublicacion)    .HasColumnName("fecha_publicacion");
        builder.Property(p => p.Modalidad)           .HasColumnName("modalidad").HasConversion<string>();
        builder.Property(p => p.Estado)              .HasColumnName("estado").HasConversion<string>();
        builder.Property(p => p.NombreEntidad)       .HasColumnName("nombre_entidad");
        builder.Property(p => p.NitEntidad)          .HasColumnName("nit_entidad");
        builder.Property(p => p.DepartamentoEntidad) .HasColumnName("departamento_entidad");
        builder.Property(p => p.UrlProceso)          .HasColumnName("url_proceso");
        builder.Property(p => p.SincronizadoEn)     .HasColumnName("sincronizado_en");

        // ── Clasificación y filtros (SPEC-01 a SPEC-09) ─────────────────────
        builder.Property(p => p.Clasificacion)       .HasColumnName("clasificacion").HasConversion<string>();
        builder.Property(p => p.TipoContrato)        .HasColumnName("tipo_contrato");

        // ── Adjudicación real ────────────────────────────────────────────────
        builder.Property(p => p.AdjudicadoA)         .HasColumnName("adjudicado_a");
        builder.Property(p => p.ValorAdjudicacion)   .HasColumnName("valor_adjudicacion").HasColumnType("decimal(18,2)");
        builder.Property(p => p.FechaAdjudicacion)   .HasColumnName("fecha_adjudicacion");

        // ── Categorías UNSPSC adicionales ────────────────────────────────────
        builder.Property(p => p.CodigoPrincipalCategoria)
            .HasColumnName("codigo_principal_categoria");

        builder.Property(p => p.CategoriasAdicionales)
            .HasColumnName("categorias_adicionales")
            .HasColumnType("text[]");

        // ── Señales de competencia ───────────────────────────────────────────
        builder.Property(p => p.ProveedoresInvitados)          .HasColumnName("proveedores_invitados");
        builder.Property(p => p.ProveedoresQueManifestaron)    .HasColumnName("proveedores_que_manifestaron");
        builder.Property(p => p.RespuestasAlProcedimiento)     .HasColumnName("respuestas_al_procedimiento");
        builder.Property(p => p.ConteoRespuestasOfertas)       .HasColumnName("conteo_respuestas_ofertas");
        builder.Property(p => p.ProveedoresUnicosCon)          .HasColumnName("proveedores_unicos_con");

        // pgvector: vector(1536) — texto embebido de Titulo + Objeto
        var vectorConverter = new ValueConverter<float[]?, Vector?>(
            v => v == null ? null : new Vector(v),
            v => v == null ? null : v.ToArray());

        builder.Property(p => p.Embedding)
            .HasColumnName("embedding")
            .HasColumnType("vector(1536)")
            .HasConversion(vectorConverter);
    }
}
