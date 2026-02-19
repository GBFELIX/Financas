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
    }

    public class CarteiraTotalViewModel // Sem atributo [Table]
    {
        public List<CarteiraItemViewModel> Itens { get; set; } = new List<CarteiraItemViewModel>();
        public decimal TotalPatrimonio => Itens.Sum(x => x.ValorAtual);
        public decimal TotalLucro => Itens.Sum(x => x.LucroPrejuizo);

        // ADICIONE ESTAS LINHAS PARA RESOLVER O ERRO DA IMAGEM 8
        public List<string> ListaTickersAcoes { get; set; } = new List<string>();
        public List<string> ListaTickersFiis { get; set; } = new List<string>();
        public List<string> ListaTickersGerais { get; set; } = new List<string>();

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
    }
}