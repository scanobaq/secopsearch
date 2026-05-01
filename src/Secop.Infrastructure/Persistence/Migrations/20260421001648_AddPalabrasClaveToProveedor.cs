using System.Collections.Generic;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Secop.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class AddPalabrasClaveToProveedor : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<List<string>>(
                name: "palabras_clave",
                table: "proveedores",
                type: "text[]",
                nullable: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "palabras_clave",
                table: "proveedores");
        }
    }
}
