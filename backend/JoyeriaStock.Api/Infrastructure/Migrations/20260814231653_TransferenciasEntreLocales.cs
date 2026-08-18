using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace JoyeriaStock.Api.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class TransferenciasEntreLocales : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AlterColumn<string>(
                name: "tipo",
                table: "movimientos",
                type: "character varying(30)",
                maxLength: 30,
                nullable: false,
                oldClrType: typeof(string),
                oldType: "character varying(20)",
                oldMaxLength: 20);

            migrationBuilder.AddColumn<Guid>(
                name: "transferencia_id",
                table: "movimientos",
                type: "uuid",
                nullable: true);

            migrationBuilder.CreateIndex(
                name: "ix_movimientos_transferencia_id",
                table: "movimientos",
                column: "transferencia_id");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "ix_movimientos_transferencia_id",
                table: "movimientos");

            migrationBuilder.DropColumn(
                name: "transferencia_id",
                table: "movimientos");

            migrationBuilder.AlterColumn<string>(
                name: "tipo",
                table: "movimientos",
                type: "character varying(20)",
                maxLength: 20,
                nullable: false,
                oldClrType: typeof(string),
                oldType: "character varying(30)",
                oldMaxLength: 30);
        }
    }
}
