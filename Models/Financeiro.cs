using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations.Schema;
using System.Linq;

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

        public string Tipo { get; set; } = string.Empty;

        public string? Categoria { get; set; }

        public string? Pagamento { get; set; }

        public string Dono { get; set; } = "Gabriel";
    }

    public class FluxoCaixaViewModel
    {
        public List<Financeiro> Lancamentos { get; set; } = new List<Financeiro>();

        public int MesAtual { get; set; }

        public int AnoAtual { get; set; }

        public decimal TotalEntradas => Lancamentos.Where(x => x.Tipo == "Entrada").Sum(x => x.Valor);

        public decimal TotalDespesas => Lancamentos.Where(x => x.Tipo == "Despesa").Sum(x => x.Valor);

        public decimal InvestimentoMesCorrente => Lancamentos.Where(x => x.Categoria == "Investimento").Sum(x => x.Valor);

        public decimal RendimentoRendaFixaMes { get; set; }

        public decimal SobraEstimada => (TotalEntradas - TotalDespesas) + RendimentoRendaFixaMes + InvestimentoMesCorrente;

        public decimal TotalAnual { get; set; }

        public decimal TotalPagoCasa { get; set; }

        public decimal TotalPendenteCasa { get; set; }

        public decimal ValorParcelaAtual { get; set; }

        public decimal TotalAmortizacaoMes { get; set; }

        public bool ParcelaPaga { get; set; }

        public List<ContaFixa> ContasFixas { get; set; } = new List<ContaFixa>();

        public decimal MediaSobraHistorica { get; set; }

        public List<ResumoMesViewModel> ResumoMensal { get; set; } = new List<ResumoMesViewModel>();

        public decimal EntradasMesCorrente { get; set; }

        public decimal SaidasMesCorrente { get; set; }

        public decimal PatrimonioTotalReal { get; set; }

        public decimal TotalRendaVariavel { get; set; }
        public int Ano { get; set; }
        public int Mes { get; set; }
        public decimal Entradas { get; set; }
        public decimal Saidas { get; set; }
    }

}