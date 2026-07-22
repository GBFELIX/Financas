using System;
using System.ComponentModel.DataAnnotations;

namespace Acoes_Fiis.Models
{
    public class PlanejamentoCompra
    {
        [Key]
        public int Id { get; set; }
        public string Ticker { get; set; }

        // A quantidade que você planeja comprar
        public int Quantidade { get; set; }

        // O preço do ativo no momento em que você colocou na lista (ou seu preço teto)
        public decimal PrecoReferencia { get; set; }

        // Status da compra
        public bool Comprado { get; set; } = false;

        public string Dono { get; set; }
        public DateTime DataPlanejamento { get; set; } = DateTime.Now;
    }
}