using System;
using System.ComponentModel.DataAnnotations.Schema;

namespace Acoes_Fiis.Models
{
    [Table("[ControleFinanceiro]")]
    public class Financeiro
    {
        // Identificador único
        public int Id { get; set; }

        // Descrição da transação
        public string Descricao { get; set; } = string.Empty;

        // Valor monetário com precisão compatível com o schema do BD
        [Column(TypeName = "decimal(18,2)")]
        public decimal Valor { get; set; }

        // Data da transação, padrão para data/hora atual
        public DateTime Data { get; set; } = DateTime.Now;

        // Tipo: "Entrada" ou "Despesa"
        public string Tipo { get; set; } = string.Empty;

        // Categoria opcional
        public string? Categoria { get; set; }
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

        // Valor que vem da Renda Fixa (calculado na Controller)
        public decimal RendimentoRendaFixaMes { get; set; }

        // O que sobra para investir no final do mês
        public decimal SobraEstimada => (TotalEntradas - TotalDespesas) + RendimentoRendaFixaMes;
    }
}
