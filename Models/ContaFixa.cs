using System.ComponentModel.DataAnnotations.Schema;

namespace Acoes_Fiis.Models
{
    public class ContaFixa
    {
        public int Id { get; set; }
        public string Descricao { get; set; }
        public decimal Valor { get; set; }

        public bool EhRecorrente { get; set; } = true;

        public string Categoria { get; set; } = "Serviços";

        public string Dono { get; set; } = "Gabriel";

        [NotMapped]
        public bool PagoNoMesAtual { get; set; }

    }
}