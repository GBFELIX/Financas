using System;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace Acoes_Fiis.Models
{
    [Table("RecomendacoesFii")]
    public class RecomendacaoFii
    {
        public int Id { get; set; }

        [Required]
        public string Ticker { get; set; }

        [Column(TypeName = "decimal(18, 2)")]
        public decimal PrecoAtual { get; set; }

        [Column(TypeName = "decimal(18, 2)")]
        public decimal VPA { get; set; }

        [Column(TypeName = "decimal(18, 2)")]
        public decimal UltimoRendimento { get; set; }

        [Column(TypeName = "decimal(18, 2)")]
        public decimal Vacancia { get; set; }

        public string TipoFii { get; set; } // Tijolo, Papel, FoF
        public string Segmento { get; set; } // Logística, Shopping, etc.

        public DateTime DataAtualizacao { get; set; }

        [NotMapped]
        public decimal DividendYieldMensal => PrecoAtual > 0 ? (UltimoRendimento / PrecoAtual) * 100 : 0;

        [NotMapped]
        public decimal PVP => VPA > 0 ? PrecoAtual / VPA : 0;

        // Dica: Em FIIs, o Dividend Yield Anualizado é muito usado na CPA-20
        [NotMapped]
        public decimal DYAnualizado => DividendYieldMensal * 12;
    }
}