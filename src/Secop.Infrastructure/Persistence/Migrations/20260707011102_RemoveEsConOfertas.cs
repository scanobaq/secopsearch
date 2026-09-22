using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Secop.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class RemoveEsConOfertas : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "es_con_ofertas",
                table: "procesos");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<bool>(
                name: "es_con_ofertas",
                table: "procesos",
                type: "boolean",
                nullable: false,
                defaultValue: false);
        }
    }
}
