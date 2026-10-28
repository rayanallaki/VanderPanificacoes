using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace VanderPanificacoes.Api.Migrations
{
    /// <inheritdoc />
    public partial class IdentificadorRecebimento : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<Guid>(
                name: "IdentificadorRecebimento",
                table: "Parcelas",
                type: "char(36)",
                nullable: true,
                collation: "ascii_general_ci");

            migrationBuilder.CreateIndex(
                name: "IX_Parcelas_IdentificadorRecebimento",
                table: "Parcelas",
                column: "IdentificadorRecebimento",
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_Parcelas_IdentificadorRecebimento",
                table: "Parcelas");

            migrationBuilder.DropColumn(
                name: "IdentificadorRecebimento",
                table: "Parcelas");
        }
    }
}
