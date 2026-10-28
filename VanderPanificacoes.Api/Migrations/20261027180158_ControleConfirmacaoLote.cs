using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace VanderPanificacoes.Api.Migrations
{
    /// <inheritdoc />
    public partial class ControleConfirmacaoLote : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "HashConfirmacao",
                table: "LotesPedidos",
                type: "varchar(64)",
                maxLength: 64,
                nullable: true)
                .Annotation("MySql:CharSet", "utf8mb4");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "HashConfirmacao",
                table: "LotesPedidos");
        }
    }
}
