using System;
using System.Collections.Generic;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Secop.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class AddSecopClassificationFields : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "adjudicado_a",
                table: "procesos",
                type: "text",
                nullable: true);

            migrationBuilder.AddColumn<List<string>>(
                name: "categorias_adicionales",
                table: "procesos",
                type: "text[]",
                nullable: false,
                defaultValue: new List<string>());

            migrationBuilder.AddColumn<string>(
                name: "clasificacion",
                table: "procesos",
                type: "text",
                nullable: false,
                defaultValue: "");

            migrationBuilder.AddColumn<int>(
                name: "conteo_respuestas_ofertas",
                table: "procesos",
                type: "integer",
                nullable: true);

            migrationBuilder.AddColumn<bool>(
                name: "es_con_ofertas",
                table: "procesos",
                type: "boolean",
                nullable: false,
                defaultValue: false);

            migrationBuilder.AddColumn<DateTime>(
                name: "fecha_adjudicacion",
                table: "procesos",
                type: "timestamp with time zone",
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "proveedores_invitados",
                table: "procesos",
                type: "integer",
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "proveedores_que_manifestaron",
                table: "procesos",
                type: "integer",
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "proveedores_unicos_con",
                table: "procesos",
                type: "integer",
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "respuestas_al_procedimiento",
                table: "procesos",
                type: "integer",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "tipo_contrato",
                table: "procesos",
                type: "text",
                nullable: true);

            migrationBuilder.AddColumn<decimal>(
                name: "valor_adjudicacion",
                table: "procesos",
                type: "numeric(18,2)",
                nullable: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "adjudicado_a",
                table: "procesos");

            migrationBuilder.DropColumn(
                name: "categorias_adicionales",
                table: "procesos");

            migrationBuilder.DropColumn(
                name: "clasificacion",
                table: "procesos");

            migrationBuilder.DropColumn(
                name: "conteo_respuestas_ofertas",
                table: "procesos");

            migrationBuilder.DropColumn(
                name: "es_con_ofertas",
                table: "procesos");

            migrationBuilder.DropColumn(
                name: "fecha_adjudicacion",
                table: "procesos");

            migrationBuilder.DropColumn(
                name: "proveedores_invitados",
                table: "procesos");

            migrationBuilder.DropColumn(
                name: "proveedores_que_manifestaron",
                table: "procesos");

            migrationBuilder.DropColumn(
                name: "proveedores_unicos_con",
                table: "procesos");

            migrationBuilder.DropColumn(
                name: "respuestas_al_procedimiento",
                table: "procesos");

            migrationBuilder.DropColumn(
                name: "tipo_contrato",
                table: "procesos");

            migrationBuilder.DropColumn(
                name: "valor_adjudicacion",
                table: "procesos");
        }
    }
}
