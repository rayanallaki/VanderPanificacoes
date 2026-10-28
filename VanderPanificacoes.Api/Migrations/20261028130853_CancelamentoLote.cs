using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace VanderPanificacoes.Api.Migrations
{
    /// <inheritdoc />
    public partial class CancelamentoLote : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<Guid>(
                name: "IdentificadorCancelamento",
                table: "LotesPedidos",
                type: "char(36)",
                nullable: true,
                collation: "ascii_general_ci");

            migrationBuilder.AddColumn<int>(
                name: "UsuarioCancelamentoId",
                table: "LotesPedidos",
                type: "int",
                nullable: true);

            migrationBuilder.CreateIndex(
                name: "IX_LotesPedidos_IdentificadorCancelamento",
                table: "LotesPedidos",
                column: "IdentificadorCancelamento",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_LotesPedidos_UsuarioCancelamentoId",
                table: "LotesPedidos",
                column: "UsuarioCancelamentoId");

            migrationBuilder.AddForeignKey(
                name: "FK_LotesPedidos_Usuarios_UsuarioCancelamentoId",
                table: "LotesPedidos",
                column: "UsuarioCancelamentoId",
                principalTable: "Usuarios",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_LotesPedidos_Usuarios_UsuarioCancelamentoId",
                table: "LotesPedidos");

            migrationBuilder.DropIndex(
                name: "IX_LotesPedidos_IdentificadorCancelamento",
                table: "LotesPedidos");

            migrationBuilder.DropIndex(
                name: "IX_LotesPedidos_UsuarioCancelamentoId",
                table: "LotesPedidos");

            migrationBuilder.DropColumn(
                name: "IdentificadorCancelamento",
                table: "LotesPedidos");

            migrationBuilder.DropColumn(
                name: "UsuarioCancelamentoId",
                table: "LotesPedidos");
        }
    }
}
