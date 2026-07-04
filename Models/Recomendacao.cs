using System;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace Acoes_Fiis.Models
{
    [Table("Recomendacoes")]
    public class Recomendacao
    {
        public int Id { get; set; }

        public string Nome { get; set; }
        public string? Setor { get; set; } = "padrao";
        public string Ticker { get; set; }

        [Column(TypeName = "decimal(18,2)")]
        public decimal PrecoAtual { get; set; }

        [Column(TypeName = "decimal(18,2)")]
        public decimal VPA { get; set; }

        [Column(TypeName = "decimal(18,2)")]
        public decimal LPA { get; set; }

        [Column(TypeName = "decimal(18,2)")]
        public decimal Roe { get; set; }

        [Column(TypeName = "decimal(18,2)")]
        public decimal DividendYield { get; set; }

        public DateTime? DataAtualizacao { get; set; }

        [Column(TypeName = "decimal(18,2)")]
        public decimal RegularMarketOpen { get; set; }

        [Column(TypeName = "decimal(18,2)")]
        public decimal RegularMarketPreviousClose { get; set; }

        [Column(TypeName = "decimal(18,2)")]
        public decimal RegularMarketDayLow { get; set; }

        [Column(TypeName = "decimal(18,2)")]
        public decimal RegularMarketDayHigh { get; set; }

        [Column(TypeName = "decimal(18,2)")]
        public decimal FiftyTwoWeekLow { get; set; }

        [Column(TypeName = "decimal(18,2)")]
        public decimal FiftyTwoWeekHigh { get; set; }

        [Column(TypeName = "decimal(18,2)")]
        public decimal ForwardPE { get; set; }

        [Column(TypeName = "decimal(18,2)")]
        public decimal PriceToBook { get; set; }

        [Column(TypeName = "decimal(18,2)")]
        public decimal MarketCap { get; set; }

        public long RegularMarketVolume { get; set; }

        [Display(Name = "Tipo de Ativo")]
        public string? TipoAtivo { get; set; }
        public string? TipoAcao { get; set; }

        public decimal PVP => VPA > 0 ? PrecoAtual / VPA : 0;
        public decimal PL => LPA > 0 ? PrecoAtual / LPA : 0;
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
                if (PrecoAtual >= PrecoTetoVenda) return 0;

                return ((PrecoTetoVenda / PrecoAtual) - 1) * 100;
            }
        }

        [NotMapped]
        public decimal ValorJustoGraham
        {
            get
            {

                if (string.Equals(TipoAtivo, "FII", StringComparison.OrdinalIgnoreCase) ||
                    string.Equals(TipoAtivo, "FundoImobiliario", StringComparison.OrdinalIgnoreCase))
                {
                    return 0;
                }

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

                if (PrecoAtual >= ValorJustoGraham) return 0;

                return ((ValorJustoGraham - PrecoAtual) / ValorJustoGraham) * 100;
            }
        }
    }
}