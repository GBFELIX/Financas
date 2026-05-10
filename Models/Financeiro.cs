using System;
using System.ComponentModel.DataAnnotations.Schema;

namespace Acoes_Fiis.Models
{
    [Table("ControleFinanceiro")]
    public class Financeiro
    {
        public int Id { get; set; }

        public string Descricao { get; set; } = string.Empty;

        [Column(TypeName = "decimal(18,2)")]
        public decimal Valor { get; set; }

        public DateTime Data { get; set; } = DateTime.Now;

        // Tipo: "Entrada" ou "Despesa"
        public string Tipo { get; set; } = string.Empty;

        public string? Categoria { get; set; }
        public string? Pagamento { get; set; }


    }
    public class FluxoCaixaViewModel
    {
        public List<Financeiro> Lancamentos { get; set; } = new List<Financeiro>();

        // Filtros
        public int MesAtual { get; set; }
        public int AnoAtual { get; set; }

        // Cálculos
        public decimal TotalEntradas => Lancamentos.Where(x => x.Tipo == "Entrada").Sum(x => x.Valor);
        public decimal TotalDespesas => Lancamentos.Where(x => x.Tipo == "Despesa").Sum(x => x.Valor);
        public decimal RendimentoRendaFixaMes { get; set; }
        public decimal SobraEstimada => (TotalEntradas - TotalDespesas) + RendimentoRendaFixaMes;

        public decimal TotalAnual { get; set; }

        //CASA

        public decimal TotalPagoCasa { get; set; } // Soma do que está com checkbox 'checked'
        public decimal TotalPendenteCasa { get; set; } // Soma do que falta pagar
        public decimal ValorParcelaAtual { get; set; } // Vem do FinanciamentoService
        public bool ParcelaPaga { get; set; } // Se já existe um lançamento de "Despesa" na categoria Moradia com esse valor
        public List<ContaFixa> ContasFixas { get; set; } // Nova tabela ou lista filtrada

    }
}
