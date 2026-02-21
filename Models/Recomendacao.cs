using System;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace Acoes_Fiis.Models
{
    [Table("Recomendacoes")]
    public class Recomendacao
    {
        public int Id { get; set; }
        public string Ticker { get; set; }
        [Column(TypeName = "decimal(18,2)")]
        public decimal PrecoAtual { get; set; }
        [Column(TypeName = "decimal(18,2)")]
        public decimal VPA { get; set; }
        [Column(TypeName = "decimal(18,2)")]// Valor Patrimonial por Ação
        public decimal LPA { get; set; }// Lucro por Ação
        [Column(TypeName = "decimal(18,2)")]
        public decimal Roe { get; set; } // Em porcentagem (ex: 15.0)
        [Column(TypeName = "decimal(18,2)")]
        public decimal DividendYield { get; set; } // Em porcentagem

        [DataType(DataType.DateTime)]
        [Column(TypeName = "datetime")]
        public DateTime DataAtualizacao { get; set; }
        // Cálculos Automáticos
        public decimal PVP => PrecoAtual / VPA;
        public decimal PL => LPA > 0 ? PrecoAtual / LPA : 0; // Preço/Lucro

        [Display(Name = "Tipo de Ativo")]

        public string? TipoAtivo { get; set; }
        public string Status
        {
            get
            {
                // Lógica de "Compra Forte"
                if (PVP < 1.0m && Roe > 10m && PL < 15m)
                    return "Compra Forte";

                // Lógica de "Compra" (Barata, mas com ROE modesto)
                if (PVP < 1.2m && Roe > 5m)
                    return "Compra";

                // Lógica de "Venda" (Muito cara ou ROE negativo)
                if (PVP > 2.5m || Roe < 0)
                    return "Venda/Risco";

                return "Aguardar/Neutro";
            }
        }

        public string CorClasse => Status switch
        {
            "Compra Forte" => "badge bg-success",
            "Compra" => "badge bg-info",
            "Venda/Risco" => "badge bg-danger",
            _ => "badge bg-secondary"
        };
        [NotMapped]
        public decimal PrecoTetoVenda
        {
            get
            {
                if (VPA <= 0) return 0;

                // Exemplo: Se o P/VP for maior que 1.5, vira "Venda"
                // Então: PrecoTeto = VPA * 1.5
                return VPA * 1.5m;
            }
        }

        [NotMapped]
        public decimal DistanciaParaVenda
        {
            get
            {
                if (PrecoAtual <= 0 || PrecoTetoVenda <= 0) return 0;
                return ((PrecoTetoVenda / PrecoAtual) - 1) * 100;
            }
        }
        [NotMapped]
        public decimal ValorJustoGraham
        {
            get
            {
                // A fórmula de Graham é: Raiz Quadrada de (22.5 * LPA * VPA)
                // Só calculamos se LPA e VPA forem positivos para evitar erro matemático
                if (LPA <= 0 || VPA <= 0) return 0;

                double resultado = Math.Sqrt(22.5 * (double)LPA * (double)VPA);
                return (decimal)resultado;
            }
        }

        [NotMapped]
        public decimal MargemSeguranca
        {
            get
            {
                if (PrecoAtual <= 0 || ValorJustoGraham <= 0) return 0;
                // Diferença percentual entre o preço atual e o valor justo
                return ((ValorJustoGraham / PrecoAtual) - 1) * 100;
            }
        }
    }
}