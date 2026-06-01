using System.ComponentModel.DataAnnotations.Schema;

namespace Acoes_Fiis.Models
{
    [Table("AportesExtras")]
    public class AporteExtra
    {
        public int Id { get; set; }
        public int MesReferencia { get; set; }
        public decimal Valor { get; set; }

        // Relacionamento com o Financiamento
        public int PriceId { get; set; }
        public Price Price { get; set; }
    }
}
