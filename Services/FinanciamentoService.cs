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

            // Tratamento preventivo para mapear a propriedade correta de saldo inicial
            decimal saldo = f.SaldoDevedorInicial > 0 ? f.SaldoDevedorInicial : (f.ValorImovel - f.ValorEntrada);
            decimal taxaMensal = f.TaxaJurosAnual / 12 / 100;

            DateTime dataContrato = f.DataInicio;

            double fator = Math.Pow(1 + (double)taxaMensal, f.PrazoMeses);

            // PRESTAÇÃO PURA DA PRICE (Sem taxas embutidas para não corromper a amortização)
            decimal prestacaoPuraPrice = saldo * (taxaMensal * (decimal)fator) / ((decimal)fator - 1);

            // Taxa operacional fixa do banco (Ex: Seguro + Taxa Adm Caixa)
            decimal taxasBancariasFixas = 54.00m;

            for (int i = 1; i <= f.PrazoMeses && saldo > 0.01m; i++)
            {
                decimal jurosNoMes = Math.Round(saldo * taxaMensal, 2);

                // Amortização matemática base real do contrato
                decimal amortizacaoNoMes = prestacaoPuraPrice - jurosNoMes;

                // Captura os aportes planejados por você e pela Suely
                decimal extraDoMes = f.AporteExtraMensal ?? 0;

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

                    // O valor cobrado na parcela real será: O que foi amortizado + Juros + Taxas de administração do banco
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