using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Secop.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class AddProcesoCodigoPrincipalCategoria : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "codigo_principal_categoria",
                table: "procesos",
                type: "text",
                nullable: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "codigo_principal_categoria",
                table: "procesos");
        }
    }
}
