
using System;

namespace Acoes_Fiis.Models
{
    public class HistoricoProvento
    {
        public int Id { get; set; }
        public string Ticker { get; set; }
        public decimal ValorTotal { get; set; }
        public DateTime DataPagamento { get; set; }
        public string Dono { get; set; }
    }
}

