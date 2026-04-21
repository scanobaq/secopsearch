using System;
using System.Collections.Generic;
using Microsoft.EntityFrameworkCore.Migrations;
using Pgvector;

#nullable disable

namespace Secop.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class InitialCreate : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AlterDatabase()
                .Annotation("Npgsql:PostgresExtension:vector", ",,");

            migrationBuilder.CreateTable(
                name: "alertas",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    proceso_id = table.Column<string>(type: "text", nullable: true),
                    proveedor_id = table.Column<Guid>(type: "uuid", nullable: true),
                    tipo = table.Column<string>(type: "text", nullable: false),
                    enviada_en = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    telegram_message_id = table.Column<int>(type: "integer", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_alertas", x => x.id);
                });

            migrationBuilder.CreateTable(
                name: "decisiones",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    proceso_id = table.Column<string>(type: "text", nullable: false),
                    proveedor_id = table.Column<Guid>(type: "uuid", nullable: false),
                    accion = table.Column<string>(type: "text", nullable: false),
                    razon_descarte = table.Column<string>(type: "text", nullable: true),
                    resultado = table.Column<string>(type: "text", nullable: true),
                    registrado_en = table.Column<DateTime>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_decisiones", x => x.id);
                });

            migrationBuilder.CreateTable(
                name: "procesos",
                columns: table => new
                {
                    id = table.Column<string>(type: "text", nullable: false),
                    titulo = table.Column<string>(type: "text", nullable: false),
                    objeto = table.Column<string>(type: "text", nullable: false),
                    presupuesto = table.Column<decimal>(type: "numeric(18,2)", nullable: false),
                    fecha_cierre = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    fecha_publicacion = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    modalidad = table.Column<string>(type: "text", nullable: false),
                    estado = table.Column<string>(type: "text", nullable: false),
                    nombre_entidad = table.Column<string>(type: "text", nullable: false),
                    nit_entidad = table.Column<string>(type: "text", nullable: false),
                    departamento_entidad = table.Column<string>(type: "text", nullable: false),
                    url_proceso = table.Column<string>(type: "text", nullable: false),
                    embedding = table.Column<Vector>(type: "vector(1536)", nullable: true),
                    sincronizado_en = table.Column<DateTime>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_procesos", x => x.id);
                });

            migrationBuilder.CreateTable(
                name: "proveedores",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    nombre = table.Column<string>(type: "text", nullable: false),
                    nit = table.Column<string>(type: "text", nullable: false),
                    rup_vigencia = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    capacidad_financiera = table.Column<decimal>(type: "numeric(18,2)", nullable: false),
                    codigos_unspsc = table.Column<List<string>>(type: "text[]", nullable: false),
                    experiencia = table.Column<List<string>>(type: "text[]", nullable: false),
                    embedding = table.Column<Vector>(type: "vector(1536)", nullable: true),
                    telegram_chat_id = table.Column<long>(type: "bigint", nullable: true),
                    creado_en = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    actualizado_en = table.Column<DateTime>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_proveedores", x => x.id);
                });

            migrationBuilder.CreateTable(
                name: "puntajes",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    proceso_id = table.Column<string>(type: "text", nullable: false),
                    proveedor_id = table.Column<Guid>(type: "uuid", nullable: false),
                    puntaje_total = table.Column<float>(type: "real", nullable: false),
                    puntaje_similitud = table.Column<float>(type: "real", nullable: false),
                    puntaje_requisitos = table.Column<float>(type: "real", nullable: false),
                    puntaje_tiempo = table.Column<float>(type: "real", nullable: false),
                    puntaje_competencia = table.Column<float>(type: "real", nullable: false),
                    puntaje_entidad = table.Column<float>(type: "real", nullable: false),
                    etiqueta = table.Column<string>(type: "text", nullable: false),
                    advertencias = table.Column<List<string>>(type: "text[]", nullable: false),
                    es_inhabilitado = table.Column<bool>(type: "boolean", nullable: false),
                    calculado_en = table.Column<DateTime>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_puntajes", x => x.id);
                });

            migrationBuilder.CreateIndex(
                name: "IX_proveedores_nit",
                table: "proveedores",
                column: "nit",
                unique: true);

            // IVFFlat index para búsqueda por similitud coseno en pgvector
            migrationBuilder.Sql(
                "CREATE INDEX ON procesos USING ivfflat (embedding vector_cosine_ops) WITH (lists = 100);");
            migrationBuilder.Sql(
                "CREATE INDEX ON proveedores USING ivfflat (embedding vector_cosine_ops) WITH (lists = 100);");

            migrationBuilder.CreateIndex(
                name: "IX_puntajes_proceso_id_proveedor_id",
                table: "puntajes",
                columns: new[] { "proceso_id", "proveedor_id" },
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "alertas");

            migrationBuilder.DropTable(
                name: "decisiones");

            migrationBuilder.DropTable(
                name: "procesos");

            migrationBuilder.DropTable(
                name: "proveedores");

            migrationBuilder.DropTable(
                name: "puntajes");
        }
    }
}
