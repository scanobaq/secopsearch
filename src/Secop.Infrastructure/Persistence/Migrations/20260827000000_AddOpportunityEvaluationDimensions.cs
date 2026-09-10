using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Secop.Infrastructure.Persistence.Migrations;

[DbContext(typeof(AppDbContext))]
[Migration("20260827000000_AddOpportunityEvaluationDimensions")]
public partial class AddOpportunityEvaluationDimensions : Migration
{
    protected override void Up(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.AddColumn<string>(
            name: "accionabilidad",
            table: "puntajes",
            type: "text",
            nullable: true);

        migrationBuilder.AddColumn<string>(
            name: "elegibilidad",
            table: "puntajes",
            type: "text",
            nullable: true);

        migrationBuilder.AddColumn<string[]>(
            name: "razones",
            table: "puntajes",
            type: "text[]",
            nullable: true);

        migrationBuilder.AddColumn<string>(
            name: "recomendacion_automatica",
            table: "puntajes",
            type: "text",
            nullable: true);

        migrationBuilder.AddColumn<float>(
            name: "relevancia_porcentaje",
            table: "puntajes",
            type: "real",
            nullable: true);

        migrationBuilder.Sql("""
            UPDATE puntajes AS evaluacion
            SET relevancia_porcentaje = LEAST(100, GREATEST(0, evaluacion.puntaje_similitud * 100 / 35)),
                elegibilidad = CASE
                    WHEN proveedor.rup_vigencia::date < CURRENT_DATE THEN 'Ineligible'
                    WHEN proceso.tipo_contrato ILIKE '%092%'
                         AND proceso.tipo_contrato ILIKE '%2017%' THEN 'Ineligible'
                    WHEN proveedor.capacidad_financiera < proceso.presupuesto * 0.15 THEN 'RequiresReview'
                    ELSE 'Eligible'
                END,
                accionabilidad = CASE
                    WHEN proceso.fecha_cierre <= TIMESTAMPTZ '1970-01-01 00:00:00+00' THEN 'UnknownDate'
                    WHEN proceso.fecha_cierre <= CURRENT_TIMESTAMP THEN 'InsufficientTime'
                    ELSE 'UnknownDate'
                END,
                razones = COALESCE(evaluacion.advertencias, ARRAY[]::text[])
                    || ARRAY['Evaluación migrada; requiere recálculo para confirmar la política vigente']
            FROM proveedores AS proveedor, procesos AS proceso
            WHERE evaluacion.proveedor_id = proveedor.id
              AND evaluacion.proceso_id = proceso.id;
            """);

        migrationBuilder.Sql("""
            UPDATE puntajes
            SET relevancia_porcentaje = COALESCE(relevancia_porcentaje, 0),
                elegibilidad = COALESCE(elegibilidad, 'RequiresReview'),
                accionabilidad = COALESCE(accionabilidad, 'UnknownDate'),
                razones = COALESCE(
                    razones,
                    ARRAY['Evaluación migrada sin asociación completa; requiere recálculo']::text[]),
                recomendacion_automatica = CASE
                    WHEN COALESCE(elegibilidad, 'RequiresReview') <> 'Ineligible'
                         AND COALESCE(accionabilidad, 'UnknownDate') <> 'InsufficientTime'
                    THEN 'Analyze'
                    ELSE NULL
                END;
            """);

        migrationBuilder.AlterColumn<string>(
            name: "accionabilidad",
            table: "puntajes",
            type: "text",
            nullable: false,
            oldClrType: typeof(string),
            oldType: "text",
            oldNullable: true);

        migrationBuilder.AlterColumn<string>(
            name: "elegibilidad",
            table: "puntajes",
            type: "text",
            nullable: false,
            oldClrType: typeof(string),
            oldType: "text",
            oldNullable: true);

        migrationBuilder.AlterColumn<string[]>(
            name: "razones",
            table: "puntajes",
            type: "text[]",
            nullable: false,
            oldClrType: typeof(string[]),
            oldType: "text[]",
            oldNullable: true);

        migrationBuilder.AlterColumn<float>(
            name: "relevancia_porcentaje",
            table: "puntajes",
            type: "real",
            nullable: false,
            oldClrType: typeof(float),
            oldType: "real",
            oldNullable: true);
    }

    protected override void Down(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.DropColumn(name: "accionabilidad", table: "puntajes");
        migrationBuilder.DropColumn(name: "elegibilidad", table: "puntajes");
        migrationBuilder.DropColumn(name: "razones", table: "puntajes");
        migrationBuilder.DropColumn(name: "recomendacion_automatica", table: "puntajes");
        migrationBuilder.DropColumn(name: "relevancia_porcentaje", table: "puntajes");
    }
}
