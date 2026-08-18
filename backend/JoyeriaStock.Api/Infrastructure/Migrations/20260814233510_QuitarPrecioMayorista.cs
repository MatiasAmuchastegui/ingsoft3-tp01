using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace JoyeriaStock.Api.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class QuitarPrecioMayorista : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "cantidad_minima_mayorista",
                table: "productos");

            migrationBuilder.DropColumn(
                name: "porcentaje_descuento_mayorista",
                table: "productos");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<int>(
                name: "cantidad_minima_mayorista",
                table: "productos",
                type: "integer",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.AddColumn<decimal>(
                name: "porcentaje_descuento_mayorista",
                table: "productos",
                type: "numeric(5,2)",
                precision: 5,
                scale: 2,
                nullable: false,
                defaultValue: 0m);
        }
    }
}
