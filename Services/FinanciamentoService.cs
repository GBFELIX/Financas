using Acoes_Fiis.Models;

namespace Acoes_Fiis.Services
{
    public class FinanciamentoService
    {
        public List<ParcelaProjecao> GerarSimulacao(Price f)
        {
            var projecao = new List<ParcelaProjecao>();
            decimal saldo = f.SaldoDevedorInicial;
            decimal taxaMensal = f.TaxaJurosAnual / 12 / 100;

            //alterar quando começar o financiamento
            DateTime dataContrato = f.DataInicio;


            double fator = Math.Pow(1 + (double)taxaMensal, f.PrazoMeses);
            decimal prestacaoFixa = f.SaldoDevedorInicial * (taxaMensal * (decimal)fator) / ((decimal)fator - 1);

            for (int i = 1; i <= f.PrazoMeses && saldo > 0.01m; i++)
            {
                decimal jurosNoMes = saldo * taxaMensal;
                decimal amortizacaoNoMes = prestacaoFixa - jurosNoMes;

                // 1. Pega o aporte extra fixo que você já tinha
                decimal extraDoMes = f.AporteExtraMensal;

                // 2. BUSCA SE TEM APORTE PONTUAL PARA ESTE MÊS ESPECÍFICO
                var aportePontual = f.AportesPontuais?.FirstOrDefault(a => a.MesReferencia == i);
                if (aportePontual != null)
                {
                    extraDoMes += aportePontual.Valor;
                }

                decimal totalAmortizado = amortizacaoNoMes + extraDoMes;

                if (totalAmortizado > saldo) totalAmortizado = saldo;

                saldo -= totalAmortizado;

                projecao.Add(new ParcelaProjecao
                {
                    Numero = i,
                    Data = dataContrato.AddMonths(i - 1),
                    ValorParcela = (totalAmortizado + jurosNoMes),
                    Amortizacao = totalAmortizado,
                    Juros = jurosNoMes,
                    SaldoDevedorRestante = Math.Max(0, saldo)
                });
            }
            return projecao;
        }
    }
}