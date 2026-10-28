using Microsoft.EntityFrameworkCore.Metadata;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

#pragma warning disable CA1814 // Prefer jagged arrays over multidimensional

namespace VanderPanificacoes.Api.Migrations
{
    /// <inheritdoc />
    public partial class CondicoesPagamento : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "CondicoesPagamento",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("MySql:ValueGenerationStrategy", MySqlValueGenerationStrategy.IdentityColumn),
                    Nome = table.Column<string>(type: "varchar(100)", maxLength: 100, nullable: false)
                        .Annotation("MySql:CharSet", "utf8mb4"),
                    QuantidadeParcelas = table.Column<int>(type: "int", nullable: false),
                    PrazosDias = table.Column<string>(type: "varchar(100)", maxLength: 100, nullable: false)
                        .Annotation("MySql:CharSet", "utf8mb4"),
                    PercentualDesconto = table.Column<decimal>(type: "decimal(5,2)", precision: 5, scale: 2, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_CondicoesPagamento", x => x.Id);
                    table.CheckConstraint("CK_Condicoes_Desconto", "`PercentualDesconto` >= 0 AND `PercentualDesconto` <= 100");
                    table.CheckConstraint("CK_Condicoes_Parcelas", "`QuantidadeParcelas` > 0");
                })
                .Annotation("MySql:CharSet", "utf8mb4");

            migrationBuilder.InsertData(
                table: "CondicoesPagamento",
                columns: new[] { "Id", "Nome", "PercentualDesconto", "PrazosDias", "QuantidadeParcelas" },
                values: new object[,]
                {
                    { 1, "À vista", 5m, "0", 1 },
                    { 2, "7/14 dias", 0m, "7,14", 2 },
                    { 3, "14/28/42 dias", 0m, "14,28,42", 3 }
                });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "CondicoesPagamento");
        }
    }
}
