using System;
using Microsoft.EntityFrameworkCore.Metadata;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace VanderPanificacoes.Api.Migrations
{
    /// <inheritdoc />
    public partial class LotesPedidos : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "LotesPedidos",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("MySql:ValueGenerationStrategy", MySqlValueGenerationStrategy.IdentityColumn),
                    IdentificadorEnvio = table.Column<Guid>(type: "char(36)", nullable: false, collation: "ascii_general_ci"),
                    HashConteudo = table.Column<string>(type: "varchar(64)", maxLength: 64, nullable: false)
                        .Annotation("MySql:CharSet", "utf8mb4"),
                    VendedorId = table.Column<int>(type: "int", nullable: false),
                    DataCriacaoLocal = table.Column<DateTime>(type: "datetime(6)", nullable: false),
                    DataRecebimento = table.Column<DateTime>(type: "datetime(6)", nullable: false),
                    DataConfirmacao = table.Column<DateTime>(type: "datetime(6)", nullable: true),
                    Situacao = table.Column<string>(type: "varchar(30)", maxLength: 30, nullable: false)
                        .Annotation("MySql:CharSet", "utf8mb4"),
                    MotivoCancelamento = table.Column<string>(type: "varchar(500)", maxLength: 500, nullable: true)
                        .Annotation("MySql:CharSet", "utf8mb4"),
                    UsuarioConfirmacaoId = table.Column<int>(type: "int", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_LotesPedidos", x => x.Id);
                    table.CheckConstraint("CK_Lotes_Situacao", "`Situacao` IN ('Recebido', 'Confirmado', 'Cancelado')");
                    table.ForeignKey(
                        name: "FK_LotesPedidos_Usuarios_UsuarioConfirmacaoId",
                        column: x => x.UsuarioConfirmacaoId,
                        principalTable: "Usuarios",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_LotesPedidos_Usuarios_VendedorId",
                        column: x => x.VendedorId,
                        principalTable: "Usuarios",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                })
                .Annotation("MySql:CharSet", "utf8mb4");

            migrationBuilder.CreateIndex(
                name: "IX_LotesPedidos_IdentificadorEnvio",
                table: "LotesPedidos",
                column: "IdentificadorEnvio",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_LotesPedidos_UsuarioConfirmacaoId",
                table: "LotesPedidos",
                column: "UsuarioConfirmacaoId");

            migrationBuilder.CreateIndex(
                name: "IX_LotesPedidos_VendedorId",
                table: "LotesPedidos",
                column: "VendedorId");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "LotesPedidos");
        }
    }
}
