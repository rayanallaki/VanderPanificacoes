using System;
using Microsoft.EntityFrameworkCore.Metadata;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace VanderPanificacoes.Api.Migrations
{
    /// <inheritdoc />
    public partial class VendasEParcelas : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<int>(
                name: "VendaId",
                table: "MovimentacoesEstoque",
                type: "int",
                nullable: true);

            migrationBuilder.CreateTable(
                name: "Vendas",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("MySql:ValueGenerationStrategy", MySqlValueGenerationStrategy.IdentityColumn),
                    PedidoId = table.Column<int>(type: "int", nullable: false),
                    DataConfirmacao = table.Column<DateTime>(type: "datetime(6)", nullable: false),
                    TotalBruto = table.Column<decimal>(type: "decimal(12,2)", precision: 12, scale: 2, nullable: false),
                    Desconto = table.Column<decimal>(type: "decimal(12,2)", precision: 12, scale: 2, nullable: false),
                    TotalLiquido = table.Column<decimal>(type: "decimal(12,2)", precision: 12, scale: 2, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Vendas", x => x.Id);
                    table.CheckConstraint("CK_Vendas_Valores", "`TotalBruto` >= 0 AND `Desconto` >= 0 AND `TotalLiquido` >= 0 AND `Desconto` <= `TotalBruto` AND `TotalLiquido` = `TotalBruto` - `Desconto`");
                    table.ForeignKey(
                        name: "FK_Vendas_Pedidos_PedidoId",
                        column: x => x.PedidoId,
                        principalTable: "Pedidos",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                })
                .Annotation("MySql:CharSet", "utf8mb4");

            migrationBuilder.CreateTable(
                name: "Parcelas",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("MySql:ValueGenerationStrategy", MySqlValueGenerationStrategy.IdentityColumn),
                    VendaId = table.Column<int>(type: "int", nullable: false),
                    Numero = table.Column<int>(type: "int", nullable: false),
                    Vencimento = table.Column<DateOnly>(type: "date", nullable: false),
                    ValorBase = table.Column<decimal>(type: "decimal(12,2)", precision: 12, scale: 2, nullable: false),
                    Situacao = table.Column<string>(type: "varchar(30)", maxLength: 30, nullable: false)
                        .Annotation("MySql:CharSet", "utf8mb4"),
                    FormaPagamentoPrevista = table.Column<string>(type: "varchar(30)", maxLength: 30, nullable: false)
                        .Annotation("MySql:CharSet", "utf8mb4"),
                    FormaPagamentoRecebida = table.Column<string>(type: "varchar(30)", maxLength: 30, nullable: true)
                        .Annotation("MySql:CharSet", "utf8mb4"),
                    DataRecebimento = table.Column<DateOnly>(type: "date", nullable: true),
                    ValorRecebido = table.Column<decimal>(type: "decimal(12,2)", precision: 12, scale: 2, nullable: true),
                    UsuarioRecebimentoId = table.Column<int>(type: "int", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Parcelas", x => x.Id);
                    table.CheckConstraint("CK_Parcelas_FormaPrevista", "`FormaPagamentoPrevista` IN ('Pix', 'Dinheiro', 'Cartao', 'Boleto')");
                    table.CheckConstraint("CK_Parcelas_FormaRecebida", "`FormaPagamentoRecebida` IS NULL OR `FormaPagamentoRecebida` IN ('Pix', 'Dinheiro', 'Cartao', 'Boleto')");
                    table.CheckConstraint("CK_Parcelas_Recebimento", "(`Situacao` = 'Aberta' AND `DataRecebimento` IS NULL AND `ValorRecebido` IS NULL AND `UsuarioRecebimentoId` IS NULL AND `FormaPagamentoRecebida` IS NULL) OR (`Situacao` = 'Paga' AND `DataRecebimento` IS NOT NULL AND `ValorRecebido` IS NOT NULL AND `ValorRecebido` = `ValorBase` AND `UsuarioRecebimentoId` IS NOT NULL AND `FormaPagamentoRecebida` IS NOT NULL)");
                    table.CheckConstraint("CK_Parcelas_Situacao", "`Situacao` IN ('Aberta', 'Paga')");
                    table.CheckConstraint("CK_Parcelas_Valores", "`Numero` > 0 AND `ValorBase` >= 0 AND (`ValorRecebido` IS NULL OR `ValorRecebido` >= 0)");
                    table.ForeignKey(
                        name: "FK_Parcelas_Usuarios_UsuarioRecebimentoId",
                        column: x => x.UsuarioRecebimentoId,
                        principalTable: "Usuarios",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_Parcelas_Vendas_VendaId",
                        column: x => x.VendaId,
                        principalTable: "Vendas",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                })
                .Annotation("MySql:CharSet", "utf8mb4");

            migrationBuilder.CreateIndex(
                name: "IX_MovimentacoesEstoque_VendaId",
                table: "MovimentacoesEstoque",
                column: "VendaId");

            migrationBuilder.CreateIndex(
                name: "IX_Parcelas_UsuarioRecebimentoId",
                table: "Parcelas",
                column: "UsuarioRecebimentoId");

            migrationBuilder.CreateIndex(
                name: "IX_Parcelas_VendaId_Numero",
                table: "Parcelas",
                columns: new[] { "VendaId", "Numero" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_Vendas_PedidoId",
                table: "Vendas",
                column: "PedidoId",
                unique: true);

            migrationBuilder.AddForeignKey(
                name: "FK_MovimentacoesEstoque_Vendas_VendaId",
                table: "MovimentacoesEstoque",
                column: "VendaId",
                principalTable: "Vendas",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_MovimentacoesEstoque_Vendas_VendaId",
                table: "MovimentacoesEstoque");

            migrationBuilder.DropTable(
                name: "Parcelas");

            migrationBuilder.DropTable(
                name: "Vendas");

            migrationBuilder.DropIndex(
                name: "IX_MovimentacoesEstoque_VendaId",
                table: "MovimentacoesEstoque");

            migrationBuilder.DropColumn(
                name: "VendaId",
                table: "MovimentacoesEstoque");
        }
    }
}
