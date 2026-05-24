using System.ComponentModel.DataAnnotations.Schema;

namespace Acoes_Fiis.Models
{
    [Table("Carteira")]
    public class Carteira
    {
        public int Id { get; set; }
        public string Ticker { get; set; } = string.Empty;
        public int Quantidade { get; set; }
        public decimal PrecoMedio { get; set; }
        public string? TipoAtivo { get; set; }
        public string? Setor { get; set; }
        public DateTime DataCompra { get; set; } = DateTime.Now;
        public decimal? TaxaRentabilidade { get; set; }
        public string Dono { get; set; } = "Gabriel";
    }

    public class CarteiraTotalViewModel
    {
        // --- Listas de Dados e Navegação ---
        public List<CarteiraItemViewModel> Itens { get; set; } = new();
        public List<ResumoMesViewModel> ResumoMensal { get; set; } = new();
        public List<RadarAporteViewModel> SugestoesAporte { get; set; } = new();

        // --- Fluxo de Caixa e Patrimônio Geral ---
        public decimal PatrimonioTotalReal { get; set; }
        public decimal EntradasFuturas { get; set; }
        public decimal SaidasFuturas { get; set; }
        public decimal EntradasMesCorrente { get; set; }
        public decimal SaidasMesCorrente { get; set; }
        public string? TipoAtivo { get; set; }

        // --- Propriedades Calculadas de Renda Variável ---
        public decimal TotalPatrimonio => Itens.Where(x => x.TipoAtivo != "RendaFixa").Sum(x => x.ValorAtual);
        public decimal TotalLucro => Itens.Where(x => x.TipoAtivo != "RendaFixa").Sum(x => x.LucroPrejuizo);
        public decimal TotalRendaMensalEstimada => Itens.Sum(x => x.ProventoMensalEstimado);

        // --- Consolidação de Renda Fixa ---
        public decimal TotalInvestidoRendaFixa { get; set; }
        public decimal TaxaMediaRendaFixa { get; set; } = 0.01m;
        public decimal RendaFixaMensalLiquida { get; set; }
        public decimal RendaMensalTotalConsolidada { get; set; }
        public decimal ProventoAnualEstimado => RendaMensalTotalConsolidada * 12;

        // --- Financiamento Imobiliário e Indicadores Estratégicos ---
        public decimal ValorImovel { get; set; }
        public decimal ValorEntrada { get; set; }
        public decimal SaldoDevedorAtual { get; set; }
        public decimal TaxaJurosAnual { get; set; }
        public int PrazoMesesRestantes { get; set; }
        public DateTime DataInicio { get; set; }
        public List<ParcelaProjecao> ProjecaoFinanciamento { get; set; } = new();

        public decimal PatrimonioLiquido => PatrimonioTotalReal - SaldoDevedorAtual;
        public decimal SobraDisponivelParaAmortizar => EntradasMesCorrente - SaidasMesCorrente;
        public double PercentualQuitacao => SaldoDevedorAtual > 0
            ? (double)(PatrimonioTotalReal / SaldoDevedorAtual) * 100
            : 100;

        // --- Listas Auxiliares (Autocomplete) ---
        public List<string> ListaTickersAcoes { get; set; } = new();
        public List<string> ListaTickersFiis { get; set; } = new();
        public List<string> ListaTickersGerais { get; set; } = new();

        public List<FolhaPagamento> HistoricoFolhas { get; set; } = new List<FolhaPagamento>();
    }

    public class CarteiraItemViewModel
    {
        public Carteira? ObjetoOriginal { get; set; }
        public int Id { get; set; }
        public string Ticker { get; set; } = string.Empty;
        public int Quantidade { get; set; }
        public decimal PrecoMedio { get; set; }
        public decimal PrecoAtual { get; set; }
        public string? TipoAtivo { get; set; }
        public decimal? TaxaRentabilidade { get; set; }

        // --- Lógica de Mercado e Recomendações ---
        public string Recomendacao { get; set; } = string.Empty;
        public string CorBadge { get; set; } = string.Empty;
        public decimal UltimoRendimento { get; set; }
        public decimal ProventoMensalEstimado => Quantidade * UltimoRendimento;

        public decimal ValorAtual
        {
            get
            {
                if (Ticker != null && Ticker.Contains("BTC"))
                {
                    return PrecoMedio;
                }
                return Quantidade * PrecoAtual;
            }
        }

        public decimal LucroPrejuizo
        {
            get
            {
                if (Ticker != null && Ticker.Contains("BTC"))
                {
                    return 0;
                }
                return ValorAtual - (Quantidade * PrecoMedio);
            }
        }
    }

    public class ResumoMesViewModel
    {
        public int Mes { get; set; }
        public int Ano { get; set; }
        public decimal Entradas { get; set; }
        public decimal Saidas { get; set; }
        public decimal Sobra => Entradas - Saidas;
    }

    public class RadarAporteViewModel
    {
        public string Ticker { get; set; } = string.Empty;
        public string Tipo { get; set; } = string.Empty; // Ação ou FII
        public decimal PrecoAtual { get; set; }
        public decimal IndicadorDesconto { get; set; } // P/VP ou P/L
        public string Mensagem { get; set; } = string.Empty;
        public decimal PotencialAporte { get; set; }
    }
    public class FolhaPagamento
    {
        public int Id { get; set; }
        public int Ano { get; set; }
        public int Mes { get; set; }
        public decimal SalarioBruto { get; set; }
        public decimal Descontos { get; set; }

        // Armazena o caminho do arquivo (ex: /uploads/contracheques/gabriel_2026_05.pdf)
        public string? PathPdf { get; set; }

        // Identifica se o registro pertence ao Gabriel ou à Suely
        public string Visao { get; set; } = "Gabriel";
        public DateTime DataRegistro { get; set; } = DateTime.Now;
    }
}