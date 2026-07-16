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

            // ====================================================================
            // CONFIGURAÇÃO DA TR (TAXA REFERENCIAL)
            // ====================================================================
            // Se você adicionar 'TaxaTrAnual' no seu modelo 'Price', substitua o valor fixo por: f.TaxaTrAnual
            decimal taxaTrAnual = 1.50m; // Estimativa média de 1.5% ao ano
            decimal taxaTrMensal = taxaTrAnual / 12 / 100; // Conversão para taxa mensal simples

            DateTime dataContrato = f.DataInicio;

            double fator = Math.Pow(1 + (double)taxaMensal, f.PrazoMeses);

            // PRESTAÇÃO PURA INICIAL DA PRICE (Sem taxas embutidas)
            decimal prestacaoPuraPrice = saldo * (taxaMensal * (decimal)fator) / ((decimal)fator - 1);

            // Taxa operacional fixa do banco (Ex: Seguro + Taxa Adm Caixa)
            decimal taxasBancariasFixas = 54.00m;

            for (int i = 1; i <= f.PrazoMeses && saldo > 0.01m; i++)
            {
                // 1. ATUALIZAÇÃO MONETÁRIA DO SALDO DEVEDOR PELA TR (Antes de rodar as contas do mês)
                decimal saldoCorrigido = Math.Round(saldo * (1 + taxaTrMensal), 2);

                // 2. REAJUSTE DA PRESTAÇÃO DO MÊS PELA TR
                prestacaoPuraPrice = Math.Round(prestacaoPuraPrice * (1 + taxaTrMensal), 2);

                // 3. CÁLCULO DOS JUROS DO MÊS SOBRE O SALDO JÁ CORRIGIDO
                decimal jurosNoMes = Math.Round(saldoCorrigido * taxaMensal, 2);

                // Amortização matemática base real do contrato (Prestação Corrigida - Juros Calculados)
                decimal amortizacaoNoMes = prestacaoPuraPrice - jurosNoMes;

                // Captura os aportes planejados por você e pela Suely
                decimal extraDoMes = f.AporteExtraMensal ?? 0;

                var aportePontual = f.AportesPontuais?.FirstOrDefault(a => a.MesReferencia == i);
                if (aportePontual != null)
                {
                    extraDoMes += aportePontual.Valor;
                }

                // A amortização total aplicada será a soma da amortização da tabela + seu aporte extra
                decimal totalAmortizado = amortizacaoNoMes + extraDoMes;

                // Segurança contra amortização maior que o próprio saldo devedor restante
                if (totalAmortizado > saldoCorrigido) totalAmortizado = saldoCorrigido;

                // O novo saldo devedor que vai para o próximo mês é o Corrigido menos o que foi amortizado de fato
                saldo = saldoCorrigido - totalAmortizado;

                projecao.Add(new ParcelaProjecao
                {
                    Numero = i,
                    Data = dataContrato.AddMonths(i - 1),

                    // O valor cobrado na parcela real será: Amortização aplicada + Juros do período + Taxas fixas do banco
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