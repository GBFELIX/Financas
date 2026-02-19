using System;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace Acoes_Fiis.Models
{
    public class AtivoGeral
    {
        public int Id { get; set; }

        [Required]
        public string Ticker { get; set; } // Ex: BTC-USD, IVVB11.SA, GOGL34.SA

        public string Nome { get; set; }

        [Column(TypeName = "decimal(18, 4)")]
        [DisplayFormat(DataFormatString = "{0:N4}", ApplyFormatInEditMode = true)]
        public decimal PrecoAtual { get; set; }

        [Required]
        public string ClasseAtivo { get; set; } // BDR, ETF, Cripto, Ouro, Moeda

        public string Moeda { get; set; } = "BRL"; // Para saber se o preço está em Reais ou Dólares

        public DateTime DataAtualizacao { get; set; }
    }
}
