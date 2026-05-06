using System.ComponentModel.DataAnnotations.Schema;

namespace Acoes_Fiis.Models
{
    [Table("Carteira")] // APENAS esta é a tabela real
    public class Carteira
    {
        public int Id { get; set; }
        public string Ticker { get; set; }
        public int Quantidade { get; set; }
        public decimal PrecoMedio { get; set; }
        public string? TipoAtivo { get; set; }
        public string? Setor { get; set; }
        public DateTime DataCompra { get; set; } = DateTime.Now;
        public decimal? TaxaRentabilidade { get; set; }
    }

    public class CarteiraTotalViewModel
    {
        public List<CarteiraItemViewModel> Itens { get; set; } = new List<CarteiraItemViewModel>();
        public List<ResumoMesViewModel> ResumoMensal { get; set; } = new List<ResumoMesViewModel>();

        public decimal PatrimonioTotalReal { get; set; }

        // Soma de tudo que entrou no Mês Atual (Salário, Bônus, etc)
        public decimal EntradasMesCorrente { get; set; }

        // Soma de tudo que saiu no Mês Atual (Contas, Compras, Lazer)
        public decimal SaidasMesCorrente { get; set; }

        public decimal TotalPatrimonio => Itens.Where(x => x.TipoAtivo != "RendaFixa").Sum(x => x.ValorAtual);
        public decimal TotalLucro => Itens.Where(x => x.TipoAtivo != "RendaFixa").Sum(x => x.LucroPrejuizo);
        public decimal TotalRendaMensalEstimada => Itens.Sum(x => x.ProventoMensalEstimado);


        public List<string> ListaTickersAcoes { get; set; } = [];
        public List<string> ListaTickersFiis { get; set; } = [];
        public List<string> ListaTickersGerais { get; set; } = [];

        public string? TipoAtivo { get; set; }





        // RENDA FIXA
        public decimal TotalInvestidoRendaFixa { get; set; }
        public decimal TaxaMediaRendaFixa { get; set; } // Ex: 0.01 (1% ao mês)

        // Cálculo de Renda Fixa Líquida (Estimando IR de 17,5% - médio prazo)
        public decimal RendaFixaMensalLiquida => (TotalInvestidoRendaFixa * TaxaMediaRendaFixa) * 0.825m;

        // TOTAIS CONSOLIDADOS
        public decimal RendaMensalTotalConsolidada => TotalRendaMensalEstimada + RendaFixaMensalLiquida;
        public decimal ProventoAnualEstimado => RendaMensalTotalConsolidada * 12;

        public decimal ProventoMensalEstimado => Quantidade * UltimoRendimento;
        public int Quantidade { get; set; }
        public decimal UltimoRendimento { get; set; }

    }

    public class CarteiraItemViewModel
    {
        public Carteira ObjetoOriginal { get; set; }
        public int Id { get; set; }
        public string Ticker { get; set; }
        public int Quantidade { get; set; }
        public decimal PrecoMedio { get; set; }
        public decimal PrecoAtual { get; set; }

        // public decimal ValorAtual => Quantidade * PrecoAtual;
        public decimal ValorAtual
        {
            get
            {
                // Se for Bitcoin, fica como valor atual
                if (Ticker != null && Ticker.Contains("BTC"))
                {
                    return PrecoMedio;
                }

                // Para os demais ativos (Ações, FIIs), mantém o cálculo real de mercado
                return Quantidade * PrecoAtual;
            }
        }
        //public decimal LucroPrejuizo => ValorAtual - (Quantidade * PrecoMedio);
        public decimal LucroPrejuizo
        {
            get
            {

                if (Ticker != null && Ticker.Contains("BTC"))
                {
                    return 0; // Para Bitcoin, não calcula lucro/prejuízo
                }

                return ValorAtual - (Quantidade * PrecoMedio);
            }
        }
        public string Recomendacao { get; set; }
        public string CorBadge { get; set; }
        public decimal UltimoRendimento { get; set; }
        public decimal ProventoMensalEstimado => Quantidade * UltimoRendimento;
        public string? TipoAtivo { get; set; }
        public decimal? TaxaRentabilidade { get; set; }
    }
    public class ResumoMesViewModel
    {
        public int Mes { get; set; }
        public int Ano { get; set; }
        public decimal Entradas { get; set; }
        public decimal Saidas { get; set; }
        public decimal Sobra => Entradas - Saidas;
    }
}