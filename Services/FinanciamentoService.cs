using Acoes_Fiis.Models;
using System;
using System.Collections.Generic;
using System.Linq;

namespace Acoes_Fiis.Services
{
    public class FinanciamentoService
    {
        public List<ParcelaProjecao> GerarSimulacao(Price f)
        {
            var projecao = new List<ParcelaProjecao>();

            decimal saldo = f.SaldoDevedorInicial > 0 ? f.SaldoDevedorInicial : (f.ValorImovel - f.ValorEntrada);
            decimal taxaMensal = f.TaxaJurosAnual / 12 / 100;

            decimal taxaTrAnual = 2.07m;
            decimal taxaTrMensal = taxaTrAnual / 12 / 100;

            DateTime dataContrato = f.DataInicio;
            decimal taxasBancariasFixas = 54.00m;

            double fator = Math.Pow(1 + (double)taxaMensal, f.PrazoMeses);
            decimal prestacaoPuraPrice = saldo * (taxaMensal * (decimal)fator) / ((decimal)fator - 1);
            prestacaoPuraPrice = Math.Round(prestacaoPuraPrice, 2);

            for (int i = 1; i <= f.PrazoMeses && saldo > 0.01m; i++)
            {
                decimal saldoCorrigido = Math.Round(saldo * (1 + taxaTrMensal), 2);

                prestacaoPuraPrice = Math.Round(prestacaoPuraPrice * (1 + taxaTrMensal), 2);

                decimal jurosNoMes = Math.Round(saldoCorrigido * taxaMensal, 2);

                decimal amortizacaoNoMes = prestacaoPuraPrice - jurosNoMes;

                decimal extraDoMes = f.AporteExtraMensal ?? 0;

                var aportePontual = f.AportesPontuais?.FirstOrDefault(a => a.MesReferencia == i);
                if (aportePontual != null)
                {
                    extraDoMes += aportePontual.Valor;
                }

                decimal totalAmortizado = amortizacaoNoMes + extraDoMes;

                if (totalAmortizado > saldoCorrigido)
                {
                    totalAmortizado = saldoCorrigido;
                }

                saldo = saldoCorrigido - totalAmortizado;

                projecao.Add(new ParcelaProjecao
                {
                    Numero = i,
                    Data = dataContrato.AddMonths(i - 1),
                    ValorParcela = Math.Round(totalAmortizado + jurosNoMes + taxasBancariasFixas, 2),
                    Amortizacao = Math.Round(totalAmortizado, 2),
                    Juros = jurosNoMes,
                    SaldoDevedorRestante = Math.Max(0, Math.Round(saldo, 2))
                });
            }
            return projecao;
        }
    }
}