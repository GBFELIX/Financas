using System.ComponentModel.DataAnnotations.Schema;

namespace Acoes_Fiis.Models
{
    [Table("Carteira")] // APENAS esta é a tabela real
    public class Carteira
    {
        public int Id { get; set; }
        public string Ticker { get; set; }
        public decimal Quantidade { get; set; }
        public decimal PrecoMedio { get; set; }
        public string? TipoAtivo { get; set; }
        public string? Setor { get; set; }
        public DateTime DataCompra { get; set; } = DateTime.Now;

        public decimal? TaxaRentabilidade { get; set; }
    }

    public class CarteiraTotalViewModel // Sem atributo [Table]
    {
        public List<CarteiraItemViewModel> Itens { get; set; } = new List<CarteiraItemViewModel>();
        public decimal TotalPatrimonio => Itens.Where(x => x.TipoAtivo != "RendaFixa").Sum(x => x.ValorAtual);
        public decimal TotalLucro => Itens.Where(x => x.TipoAtivo != "RendaFixa").Sum(x => x.LucroPrejuizo);
        public decimal TotalRendaMensalEstimada => Itens.Sum(x => x.ProventoMensalEstimado);

        // ADICIONE ESTAS LINHAS PARA RESOLVER O ERRO DA IMAGEM 8
        public List<string> ListaTickersAcoes { get; set; } = new List<string>();
        public List<string> ListaTickersFiis { get; set; } = new List<string>();
        public List<string> ListaTickersGerais { get; set; } = new List<string>();

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
        public decimal Quantidade { get; set; }
        public decimal UltimoRendimento { get; set; }

    }

    public class CarteiraItemViewModel // Sem atributo [Table]
    {
        public int Id { get; set; }
        public string Ticker { get; set; }
        public decimal Quantidade { get; set; }
        public decimal PrecoMedio { get; set; }
        public decimal PrecoAtual { get; set; }
        public decimal ValorAtual => Quantidade * PrecoAtual;
        public decimal LucroPrejuizo => ValorAtual - (Quantidade * PrecoMedio);
        public string Recomendacao { get; set; }
        public string CorBadge { get; set; }
        public decimal UltimoRendimento { get; set; } // Valor por cota (ex: R$ 0,10)
        public decimal ProventoMensalEstimado => Quantidade * UltimoRendimento; // Total que você recebe
        public string? TipoAtivo { get; set; }
        public decimal? TaxaRentabilidade { get; set; }
    }
}