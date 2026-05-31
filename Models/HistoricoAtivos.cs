using System.ComponentModel.DataAnnotations.Schema;

namespace Acoes_Fiis.Models
{
    [Table("HistoricoAtivos")]
    public class HistoricoAtivo
    {
        public int Id { get; set; }
        public string Ticker { get; set; }
        public string TipoOperacao { get; set; } // "Compra" ou "Venda"
        public int Quantidade { get; set; }
        public decimal PrecoUnidade { get; set; }
        public decimal ValorTotal => Quantidade * PrecoUnidade;
        public DateTime DataOperacao { get; set; }
        public string Dono { get; set; } // "Gabriel", "Ela" ou "Casal"
    }
}
