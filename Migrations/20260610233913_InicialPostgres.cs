using System;
using Microsoft.EntityFrameworkCore.Migrations;
using Npgsql.EntityFrameworkCore.PostgreSQL.Metadata;

#nullable disable

namespace Acoes_Fiis.Migrations
{
    /// <inheritdoc />
    public partial class InicialPostgres : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "AtivosGerais",
                columns: table => new
                {
                    Id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    Ticker = table.Column<string>(type: "text", nullable: false),
                    Nome = table.Column<string>(type: "text", nullable: false),
                    PrecoAtual = table.Column<decimal>(type: "numeric(18,2)", nullable: false),
                    ClasseAtivo = table.Column<string>(type: "text", nullable: false),
                    Moeda = table.Column<string>(type: "text", nullable: false),
                    DataAtualizacao = table.Column<DateTime>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_AtivosGerais", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "Carteira",
                columns: table => new
                {
                    Id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    Ticker = table.Column<string>(type: "text", nullable: false),
                    Quantidade = table.Column<int>(type: "integer", nullable: false),
                    PrecoMedio = table.Column<decimal>(type: "numeric", nullable: false),
                    TipoAtivo = table.Column<string>(type: "text", nullable: true),
                    Setor = table.Column<string>(type: "text", nullable: true),
                    DataCompra = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    TaxaRentabilidade = table.Column<decimal>(type: "numeric", nullable: true),
                    Dono = table.Column<string>(type: "text", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Carteira", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "ConfiguracoesBackup",
                columns: table => new
                {
                    Id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    CaminhoPastaLocal = table.Column<string>(type: "text", nullable: false),
                    IntervaloHoras = table.Column<int>(type: "integer", nullable: false),
                    UltimoBackup = table.Column<DateTime>(type: "timestamp with time zone", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ConfiguracoesBackup", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "ContasFixas",
                columns: table => new
                {
                    Id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    Descricao = table.Column<string>(type: "text", nullable: false),
                    Valor = table.Column<decimal>(type: "numeric", nullable: false),
                    EhRecorrente = table.Column<bool>(type: "boolean", nullable: false),
                    Categoria = table.Column<string>(type: "text", nullable: false),
                    Dono = table.Column<string>(type: "text", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ContasFixas", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "ControleFinanceiro",
                columns: table => new
                {
                    Id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    Descricao = table.Column<string>(type: "text", nullable: false),
                    Valor = table.Column<decimal>(type: "numeric(18,2)", nullable: false),
                    Data = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    Tipo = table.Column<string>(type: "text", nullable: false),
                    Categoria = table.Column<string>(type: "text", nullable: true),
                    Pagamento = table.Column<string>(type: "text", nullable: true),
                    Dono = table.Column<string>(type: "text", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ControleFinanceiro", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "EvolucaoPatrimonial",
                columns: table => new
                {
                    Id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    MesAno = table.Column<string>(type: "text", nullable: false),
                    PatrimonioLiquido = table.Column<decimal>(type: "numeric", nullable: false),
                    Dono = table.Column<string>(type: "text", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_EvolucaoPatrimonial", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "Financiamentos",
                columns: table => new
                {
                    Id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    Dono = table.Column<string>(type: "text", nullable: false),
                    ValorImovel = table.Column<decimal>(type: "numeric", nullable: false),
                    ValorEntrada = table.Column<decimal>(type: "numeric", nullable: false),
                    TaxaJurosAnual = table.Column<decimal>(type: "numeric", nullable: false),
                    PrazoMeses = table.Column<int>(type: "integer", nullable: false),
                    AporteExtraMensal = table.Column<decimal>(type: "numeric", nullable: true),
                    ValorPrestação = table.Column<decimal>(type: "numeric", nullable: true),
                    DataInicio = table.Column<DateTime>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Financiamentos", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "FolhasPagamento",
                columns: table => new
                {
                    Id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    Ano = table.Column<int>(type: "integer", nullable: false),
                    Mes = table.Column<int>(type: "integer", nullable: false),
                    SalarioBruto = table.Column<decimal>(type: "numeric", nullable: false),
                    Descontos = table.Column<decimal>(type: "numeric", nullable: false),
                    PathPdf = table.Column<string>(type: "text", nullable: true),
                    Visao = table.Column<string>(type: "text", nullable: false),
                    DataRegistro = table.Column<DateTime>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_FolhasPagamento", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "HistoricoAtivos",
                columns: table => new
                {
                    Id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    Ticker = table.Column<string>(type: "text", nullable: false),
                    TipoOperacao = table.Column<string>(type: "text", nullable: false),
                    Quantidade = table.Column<int>(type: "integer", nullable: false),
                    PrecoUnidade = table.Column<decimal>(type: "numeric", nullable: false),
                    DataOperacao = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    Dono = table.Column<string>(type: "text", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_HistoricoAtivos", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "MetasAlocacao",
                columns: table => new
                {
                    Id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    Categoria = table.Column<string>(type: "text", nullable: false),
                    PercentualAlvo = table.Column<decimal>(type: "numeric", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_MetasAlocacao", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "Parametros",
                columns: table => new
                {
                    Id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    CdiAnual = table.Column<decimal>(type: "numeric", nullable: false),
                    DataAtualizacao = table.Column<DateTime>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Parametros", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "Recomendacoes",
                columns: table => new
                {
                    Id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    Ticker = table.Column<string>(type: "text", nullable: false),
                    PrecoAtual = table.Column<decimal>(type: "numeric(18,2)", nullable: false),
                    VPA = table.Column<decimal>(type: "numeric(18,2)", nullable: false),
                    LPA = table.Column<decimal>(type: "numeric(18,2)", nullable: false),
                    Roe = table.Column<decimal>(type: "numeric(18,2)", nullable: false),
                    DividendYield = table.Column<decimal>(type: "numeric(18,2)", nullable: false),
                    DataAtualizacao = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    TipoAtivo = table.Column<string>(type: "text", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Recomendacoes", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "RecomendacoesFii",
                columns: table => new
                {
                    Id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    Ticker = table.Column<string>(type: "text", nullable: false),
                    PrecoAtual = table.Column<decimal>(type: "numeric(18,2)", nullable: false),
                    VPA = table.Column<decimal>(type: "numeric(18,2)", nullable: false),
                    UltimoRendimento = table.Column<decimal>(type: "numeric(18,2)", nullable: false),
                    Vacancia = table.Column<decimal>(type: "numeric(18,2)", nullable: false),
                    TipoFii = table.Column<string>(type: "text", nullable: false),
                    Segmento = table.Column<string>(type: "text", nullable: false),
                    DataAtualizacao = table.Column<DateTime>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_RecomendacoesFii", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "AportesExtras",
                columns: table => new
                {
                    Id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    MesReferencia = table.Column<int>(type: "integer", nullable: false),
                    Valor = table.Column<decimal>(type: "numeric", nullable: false),
                    PriceId = table.Column<int>(type: "integer", nullable: false)
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
                name: "ConfiguracoesBackup");

            migrationBuilder.DropTable(
                name: "ContasFixas");

            migrationBuilder.DropTable(
                name: "ControleFinanceiro");

            migrationBuilder.DropTable(
                name: "EvolucaoPatrimonial");

            migrationBuilder.DropTable(
                name: "FolhasPagamento");

            migrationBuilder.DropTable(
                name: "HistoricoAtivos");

            migrationBuilder.DropTable(
                name: "MetasAlocacao");

            migrationBuilder.DropTable(
                name: "Parametros");

            migrationBuilder.DropTable(
                name: "Recomendacoes");

            migrationBuilder.DropTable(
                name: "RecomendacoesFii");

            migrationBuilder.DropTable(
                name: "Financiamentos");
        }
    }
}
