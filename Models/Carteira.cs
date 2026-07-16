using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations.Schema;
using System.Linq;

namespace Acoes_Fiis.Models
{
    [Table("Carteira")]
    public class Carteira
    {
        public int Id { get; set; }
        public string Ticker { get; set; } = string.Empty;
        public int Quantidade { get; set; }

        [Column(TypeName = "decimal(18,2)")]
        public decimal PrecoMedio { get; set; }

        public string? TipoAtivo { get; set; }
        public string? Setor { get; set; }

        public bool Favorito { get; set; } = false;
        public DateTime DataCompra { get; set; } = DateTime.Now;

        [Column(TypeName = "decimal(18,2)")]
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
        public double PercentualAtualAcoes { get; set; }
        public double PercentualAtualFiis { get; set; }
        public double PercentualAtualRendaFixa { get; set; }

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
            ? (double)((PatrimonioTotalReal) / SaldoDevedorAtual) * 100
            : 100;

        // --- Listas Auxiliares (Autocomplete) ---
        public List<Recomendacao> ListaTickersAcoes { get; set; } = new();
        public List<RecomendacaoFii> ListaTickersFiis { get; set; } = new();
        public List<AtivoGeral> ListaTickersGerais { get; set; } = new();
        public List<HistoricoAtivo> HistoricoTransacoes { get; set; } = new();
        public List<FolhaPagamento> HistoricoFolhas { get; set; } = new();

        public Parametro Parametro { get; set; } = new();
        public ConfiguracaoBackup ConfiguracaoBackups { get; set; } = new();
        public MetaAlocacao MetaAlocacao { get; set; } = new();
        public List<Financeiro> Lancamentos { get; set; } = new();
        public decimal InvestimentoMesCorrente => Lancamentos.Where(x => x.Categoria == "Investimento" && x.Tipo == "Entrada").Sum(x => x.Valor);

        public string SugestaoAporteCategoria { get; set; } = string.Empty;
        public string SugestaoAporteJustificativa { get; set; } = string.Empty;
    }

    public class EvolucaoPatrimonialDto
    {
        public string MesAno { get; set; }
        public decimal Patrimonio { get; set; }
        public int Ano => int.TryParse(MesAno?.Substring(3, 4), out var a) ? a : 0;
        public int Mes => int.TryParse(MesAno?.Substring(0, 2), out var m) ? m : 0;
    }

    public class TransacaoDto
    {
        public DateTime Data { get; set; }
        public string Tipo { get; set; }
        public decimal Valor { get; set; }
        public string Categoria { get; set; }
        public string Descricao { get; set; }
    }

    public class ItemRebalanceamentoViewModel
    {
        public string Categoria { get; set; }
        public decimal PercentualAlvo { get; set; }
        public decimal ValorAtual { get; set; }
        public decimal PercentualAtual { get; set; }
        public decimal Desvio { get; set; }
        public string Status { get; set; }
    }

    public class CarteiraItemViewModel
    {
        public Carteira? ObjetoOriginal { get; set; }
        public int Id { get; set; }
        public string Ticker { get; set; } = string.Empty;
        public int Quantidade { get; set; }
        public decimal PrecoMedio { get; set; }
        public decimal PrecoAtual { get; set; }
        public bool Favorito { get; set; } = false;
        public decimal DividendYield { get; set; }
        public string? TipoAtivo { get; set; }
        public decimal? TaxaRentabilidade { get; set; }
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
        public decimal TotalPendenteCasa { get; set; }
    }

    public class RadarAporteViewModel
    {
        public string Ticker { get; set; } = string.Empty;
        public string Tipo { get; set; } = string.Empty;
        public decimal PrecoAtual { get; set; }
        public decimal IndicadorDesconto { get; set; }
        public string Mensagem { get; set; } = string.Empty;
        public decimal PotencialAporte { get; set; }
    }

    public class FolhaPagamento
    {
        public int Id { get; set; }
        public int Ano { get; set; }
        public int Mes { get; set; }

        [Column(TypeName = "decimal(18,2)")]
        public decimal SalarioBruto { get; set; }

        [Column(TypeName = "decimal(18,2)")]
        public decimal Descontos { get; set; }

        public string? PathPdf { get; set; }
        public string Visao { get; set; } = "Gabriel";
        public DateTime DataRegistro { get; set; } = DateTime.Now;
    }
}