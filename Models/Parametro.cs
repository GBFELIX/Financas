using System.ComponentModel.DataAnnotations.Schema;

namespace Acoes_Fiis.Models
{
    [Table("Parametros")]
    public class Parametro
    {
        public int Id { get; set; }
        public decimal CdiAnual { get; set; }
        public DateTime DataAtualizacao { get; set; }
    }
}
