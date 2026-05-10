using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace Acoes_Fiis.Models
{
    public class Price
    {
        public int Id { get; set; }
        public decimal ValorImovel { get; set; }
        public decimal ValorEntrada { get; set; }
        public decimal TaxaJurosAnual { get; set; }
        public int PrazoMeses { get; set; }
        public decimal AporteExtraMensal { get; set; }
        public decimal ValorPrestação { get; set; }

        [Display(Name = "Data da Primeira Parcela")]
        [DataType(DataType.Date)]
        public DateTime DataInicio { get; set; } = DateTime.Now;

        public decimal SaldoDevedorInicial => ValorImovel - ValorEntrada;

        // Adicione esta propriedade para a Service preencher
        [NotMapped] // Importante: System.ComponentModel.DataAnnotations.Schema
        public List<ParcelaProjecao> Projecao { get; set; } = new List<ParcelaProjecao>();

        public List<AporteExtra> AportesPontuais { get; set; } = new List<AporteExtra>();
    }
}

public class ParcelaProjecao
{
    public int Numero { get; set; }
    public DateTime Data { get; set; }
    public decimal ValorParcela { get; set; }
    public decimal Amortizacao { get; set; }
    public decimal Juros { get; set; }
    public decimal SaldoDevedorRestante { get; set; }


}

