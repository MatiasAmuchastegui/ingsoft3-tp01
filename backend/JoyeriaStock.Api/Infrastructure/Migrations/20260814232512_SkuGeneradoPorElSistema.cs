using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace JoyeriaStock.Api.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class SkuGeneradoPorElSistema : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "codigo_linea",
                table: "productos",
                type: "character varying(6)",
                maxLength: 6,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "prefijo_sku",
                table: "categorias",
                type: "character varying(6)",
                maxLength: 6,
                nullable: false,
                defaultValue: "");

            // Las categorías que ya existían quedarían todas con prefijo vacío, y el índice
            // único de más abajo fallaría. Se les deriva un prefijo de su nombre: las tres
            // primeras letras o números, en mayúsculas (Anillos -> ANI, Relojes -> REL).
            migrationBuilder.Sql("""
                UPDATE categorias
                SET prefijo_sku = COALESCE(
                        NULLIF(UPPER(SUBSTRING(REGEXP_REPLACE(nombre, '[^a-zA-Z0-9]', '', 'g') FROM 1 FOR 3)), ''),
                        'CAT' || id::text)
                WHERE prefijo_sku = '';
                """);

            // Si dos nombres derivaron el mismo prefijo (Aros y Argollas -> ARO), se desempata
            // con el id de la categoría más nueva, que es único por definición.
            migrationBuilder.Sql("""
                UPDATE categorias c
                SET prefijo_sku = LEFT(c.prefijo_sku, 3) || c.id::text
                WHERE EXISTS (
                    SELECT 1 FROM categorias o
                    WHERE o.prefijo_sku = c.prefijo_sku AND o.id < c.id);
                """);

            migrationBuilder.CreateIndex(
                name: "ix_categorias_prefijo_sku",
                table: "categorias",
                column: "prefijo_sku",
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "ix_categorias_prefijo_sku",
                table: "categorias");

            migrationBuilder.DropColumn(
                name: "codigo_linea",
                table: "productos");

            migrationBuilder.DropColumn(
                name: "prefijo_sku",
                table: "categorias");
        }
    }
}
