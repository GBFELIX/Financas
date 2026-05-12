using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Acoes_Fiis.Migrations
{
    /// <inheritdoc />
    public partial class InitialCreateSQLite : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "AtivosGerais",
                columns: table => new
                {
                    Id = table.Column<int>(type: "INTEGER", nullable: false)
                        .Annotation("Sqlite:Autoincrement", true),
                    Ticker = table.Column<string>(type: "TEXT", nullable: false),
                    Nome = table.Column<string>(type: "TEXT", nullable: false),
                    PrecoAtual = table.Column<decimal>(type: "decimal(18, 2)", nullable: false),
                    ClasseAtivo = table.Column<string>(type: "TEXT", nullable: false),
                    Moeda = table.Column<string>(type: "TEXT", nullable: false),
                    DataAtualizacao = table.Column<DateTime>(type: "TEXT", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_AtivosGerais", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "Carteira",
                columns: table => new
                {
                    Id = table.Column<int>(type: "INTEGER", nullable: false)
                        .Annotation("Sqlite:Autoincrement", true),
                    Ticker = table.Column<string>(type: "TEXT", nullable: false),
                    Quantidade = table.Column<int>(type: "INTEGER", nullable: false),
                    PrecoMedio = table.Column<decimal>(type: "TEXT", nullable: false),
                    TipoAtivo = table.Column<string>(type: "TEXT", nullable: true),
                    Setor = table.Column<string>(type: "TEXT", nullable: true),
                    DataCompra = table.Column<DateTime>(type: "TEXT", nullable: false),
                    TaxaRentabilidade = table.Column<decimal>(type: "TEXT", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Carteira", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "ContasFixas",
                columns: table => new
                {
                    Id = table.Column<int>(type: "INTEGER", nullable: false)
                        .Annotation("Sqlite:Autoincrement", true),
                    Descricao = table.Column<string>(type: "TEXT", nullable: false),
                    Valor = table.Column<decimal>(type: "TEXT", nullable: false),
                    EhRecorrente = table.Column<bool>(type: "INTEGER", nullable: false),
                    Categoria = table.Column<string>(type: "TEXT", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ContasFixas", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "ControleFinanceiro",
                columns: table => new
                {
                    Id = table.Column<int>(type: "INTEGER", nullable: false)
                        .Annotation("Sqlite:Autoincrement", true),
                    Descricao = table.Column<string>(type: "TEXT", nullable: false),
                    Valor = table.Column<decimal>(type: "decimal(18,2)", nullable: false),
                    Data = table.Column<DateTime>(type: "TEXT", nullable: false),
                    Tipo = table.Column<string>(type: "TEXT", nullable: false),
                    Categoria = table.Column<string>(type: "TEXT", nullable: true),
                    Pagamento = table.Column<string>(type: "TEXT", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ControleFinanceiro", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "Financiamentos",
                columns: table => new
                {
                    Id = table.Column<int>(type: "INTEGER", nullable: false)
                        .Annotation("Sqlite:Autoincrement", true),
                    ValorImovel = table.Column<decimal>(type: "TEXT", nullable: false),
                    ValorEntrada = table.Column<decimal>(type: "TEXT", nullable: false),
                    TaxaJurosAnual = table.Column<decimal>(type: "TEXT", nullable: false),
                    PrazoMeses = table.Column<int>(type: "INTEGER", nullable: false),
                    AporteExtraMensal = table.Column<decimal>(type: "TEXT", nullable: false),
                    ValorPrestação = table.Column<decimal>(type: "TEXT", nullable: false),
                    DataInicio = table.Column<DateTime>(type: "TEXT", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Financiamentos", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "Recomendacoes",
                columns: table => new
                {
                    Id = table.Column<int>(type: "INTEGER", nullable: false)
                        .Annotation("Sqlite:Autoincrement", true),
                    Ticker = table.Column<string>(type: "TEXT", nullable: false),
                    PrecoAtual = table.Column<decimal>(type: "decimal(18,2)", nullable: false),
                    VPA = table.Column<decimal>(type: "decimal(18,2)", nullable: false),
                    LPA = table.Column<decimal>(type: "decimal(18,2)", nullable: false),
                    Roe = table.Column<decimal>(type: "decimal(18,2)", nullable: false),
                    DividendYield = table.Column<decimal>(type: "decimal(18,2)", nullable: false),
                    DataAtualizacao = table.Column<DateTime>(type: "datetime", nullable: false),
                    TipoAtivo = table.Column<string>(type: "TEXT", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Recomendacoes", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "RecomendacoesFii",
                columns: table => new
                {
                    Id = table.Column<int>(type: "INTEGER", nullable: false)
                        .Annotation("Sqlite:Autoincrement", true),
                    Ticker = table.Column<string>(type: "TEXT", nullable: false),
                    PrecoAtual = table.Column<decimal>(type: "decimal(18, 2)", nullable: false),
                    VPA = table.Column<decimal>(type: "decimal(18, 2)", nullable: false),
                    UltimoRendimento = table.Column<decimal>(type: "decimal(18, 2)", nullable: false),
                    Vacancia = table.Column<decimal>(type: "decimal(18, 2)", nullable: false),
                    TipoFii = table.Column<string>(type: "TEXT", nullable: false),
                    Segmento = table.Column<string>(type: "TEXT", nullable: false),
                    DataAtualizacao = table.Column<DateTime>(type: "TEXT", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_RecomendacoesFii", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "AportesExtras",
                columns: table => new
                {
                    Id = table.Column<int>(type: "INTEGER", nullable: false)
                        .Annotation("Sqlite:Autoincrement", true),
                    MesReferencia = table.Column<int>(type: "INTEGER", nullable: false),
                    Valor = table.Column<decimal>(type: "TEXT", nullable: false),
                    PriceId = table.Column<int>(type: "INTEGER", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_AportesExtras", x => x.Id);
                    table.ForeignKey(
                        name: "FK_AportesExtras_Financiamentos_PriceId",
                        column: x => x.PriceId,
                        principalTable: "Financiamentos",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_AportesExtras_PriceId",
                table: "AportesExtras",
                column: "PriceId");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "AportesExtras");

            migrationBuilder.DropTable(
                name: "AtivosGerais");

            migrationBuilder.DropTable(
                name: "Carteira");

            migrationBuilder.DropTable(
                name: "ContasFixas");

            migrationBuilder.DropTable(
                name: "ControleFinanceiro");

            migrationBuilder.DropTable(
                name: "Recomendacoes");

            migrationBuilder.DropTable(
                name: "RecomendacoesFii");

            migrationBuilder.DropTable(
                name: "Financiamentos");
        }
    }
}
