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
        public DateTime DataAtualizacao { get; set; }
        public decimal PVP => PrecoAtual / VPA;
        public decimal PL => LPA > 0 ? PrecoAtual / LPA : 0; // Preço/Lucro

        [Display(Name = "Tipo de Ativo")]

        public string? TipoAtivo { get; set; }
        public string? TipoAcao { get; set; }
        public string Status
        {
            get
            {
                // 1. Risco/Venda sempre no topo (Filtro de Segurança Inicial)
                if (PVP > 2.5m || Roe < 0)
                    return "Venda/Risco";

                // 2. Lógica de "Compra Forte"
                if (PVP < 1.0m && Roe > 10m && PL < 15m)
                    return "Compra Forte";

                // 3. Lógica de "Compra" (Modesta)
                if (PVP < 1.2m && Roe > 5m)
                    return "Compra";

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
                return VPA * 1.5m;
            }
        }

        [NotMapped]
        public decimal DistanciaParaVenda
        {
            get
            {
                if (PrecoAtual <= 0 || PrecoTetoVenda <= 0) return 0;
                // Se o preço atual já passou o teto de venda, a distância é zero (já deveria vender)
                if (PrecoAtual >= PrecoTetoVenda) return 0;

                return ((PrecoTetoVenda / PrecoAtual) - 1) * 100;
            }
        }

        [NotMapped]
        public decimal ValorJustoGraham
        {
            get
            {
                // Correção Conceitual: Se for FII, a fórmula de Graham não se aplica
                // Caso você tenha a propriedade TipoAtivo na classe (como visto na imagem_c42d10.png)
                if (TipoAtivo == "FII" || TipoAtivo == "FundoImobiliario") return 0;

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

                // Se o preço atual for maior ou igual ao preço justo, não há margem de segurança (desconto)
                if (PrecoAtual >= ValorJustoGraham) return 0;

                // Fórmula real da Margem de Desconto: (Valor Justo - Preço Atual) / Valor Justo
                return ((ValorJustoGraham - PrecoAtual) / ValorJustoGraham) * 100;
            }
        }
    }
}