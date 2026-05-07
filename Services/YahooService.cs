using Acoes_Fiis.Models;
using System;
using System.Threading.Tasks;
using YahooFinanceApi;

namespace Acoes_Fiis.Services
{
    public class YahooService
    {
        public async Task<Recomendacao> ObterDadosAtivo(string ticker)
        {
            // 1. Ajusta o Ticker para o padrão do Yahoo Brasil (.SA)
            string simbolo = ticker.EndsWith(".SA") ? ticker : $"{ticker}.SA";

            // 2. Consulta os campos no Yahoo
            var security = await Yahoo.Symbols(simbolo)
                .Fields(Field.RegularMarketPrice, Field.BookValue, Field.TrailingPE)
                .QueryAsync();

            if (!security.ContainsKey(simbolo))
                throw new Exception("Ativo não encontrado no Yahoo Finance.");

            var dados = security[simbolo];

            // 3. Lógica de Proteção: Tenta obter os valores do dicionário interno 'Fields'
            // Se o campo não existir (comum em FIIs), a variável recebe null
            dados.Fields.TryGetValue(Field.RegularMarketPrice.ToString(), out object precoRaw);
            dados.Fields.TryGetValue(Field.BookValue.ToString(), out object vpaRaw);
            dados.Fields.TryGetValue(Field.TrailingPE.ToString(), out object lpaRaw);

            // 4. Converte os valores 
            decimal preco = Convert.ToDecimal(precoRaw ?? 0m);
            decimal vpa = Convert.ToDecimal(vpaRaw ?? 0m);
            decimal lpa = Convert.ToDecimal(lpaRaw ?? 0m);

            return new Recomendacao
            {
                Ticker = ticker.Replace(".SA", ""),
                PrecoAtual = preco,
                VPA = vpa,
                LPA = lpa,
                // Calcula o ROE apenas se houver VPA para evitar erro de divisão por zero
                Roe = vpa > 0 ? (lpa / vpa) * 100 : 0
            };
        }

        // Método auxiliar para Ativos Gerais (Cripto/ETF) que você já tem
        public async Task<decimal> ObterPrecoSimples(string ticker)
        {
            var security = await Yahoo.Symbols(ticker)
                .Fields(Field.RegularMarketPrice)
                .QueryAsync();

            if (!security.ContainsKey(ticker))
                throw new Exception("Ativo não encontrado.");

            var dados = security[ticker];
            return Convert.ToDecimal(dados.RegularMarketPrice);
        }
    }
}