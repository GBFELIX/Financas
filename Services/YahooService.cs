using Acoes_Fiis.Models;
using System;
using System.Threading.Tasks;
using YahooFinanceApi;
using static System.Net.Mime.MediaTypeNames;

namespace Acoes_Fiis.Services
{
    public class YahooService
    {
        public async Task<Recomendacao> ObterDadosAtivo(string ticker)
        {
            string simbolo = ticker.EndsWith(".SA") ? ticker : $"{ticker}.SA";

            var security = await Yahoo.Symbols(simbolo)
                .Fields(
                    Field.RegularMarketPrice,
                    Field.BookValue,
                    Field.TrailingPE,
                    Field.LongName,
                    Field.ShortName,
                    Field.TrailingAnnualDividendYield,
                    Field.RegularMarketVolume,
                    Field.RegularMarketDayHigh,
                    Field.RegularMarketDayLow,
                    Field.FiftyTwoWeekHigh,
                    Field.FiftyTwoWeekLow,
                    Field.MarketCap,
                    Field.ForwardPE,
                    Field.PriceToBook,
                    Field.RegularMarketOpen,
                    Field.RegularMarketPreviousClose
                )
                .QueryAsync();

            if (!security.ContainsKey(simbolo))
                throw new Exception("Ativo não encontrado no Yahoo Finance.");

            var dados = security[simbolo];

            dados.Fields.TryGetValue(Field.RegularMarketPrice.ToString(), out object precoRaw);
            dados.Fields.TryGetValue(Field.BookValue.ToString(), out object vpaRaw);
            dados.Fields.TryGetValue(Field.TrailingPE.ToString(), out object lpaRaw);
            dados.Fields.TryGetValue(Field.LongName.ToString(), out object nomeRaw);
            dados.Fields.TryGetValue(Field.ShortName.ToString(), out object setorRaw);
            dados.Fields.TryGetValue(Field.TrailingAnnualDividendYield.ToString(), out object dyRaw);
            dados.Fields.TryGetValue(Field.RegularMarketVolume.ToString(), out object volumeRaw);
            dados.Fields.TryGetValue(Field.RegularMarketDayHigh.ToString(), out object maxDiaRaw);
            dados.Fields.TryGetValue(Field.RegularMarketDayLow.ToString(), out object minDiaRaw);
            dados.Fields.TryGetValue(Field.FiftyTwoWeekHigh.ToString(), out object max52SemanasRaw);
            dados.Fields.TryGetValue(Field.FiftyTwoWeekLow.ToString(), out object min52SemanasRaw);
            dados.Fields.TryGetValue(Field.MarketCap.ToString(), out object valorMercadoRaw);
            dados.Fields.TryGetValue(Field.ForwardPE.ToString(), out object plProjetadoRaw);
            dados.Fields.TryGetValue(Field.PriceToBook.ToString(), out object pvpRaw);
            dados.Fields.TryGetValue(Field.RegularMarketOpen.ToString(), out object aberturaRaw);
            dados.Fields.TryGetValue(Field.RegularMarketPreviousClose.ToString(), out object fechamentoRaw);

            decimal preco = Convert.ToDecimal(precoRaw ?? 0m);
            decimal vpa = Convert.ToDecimal(vpaRaw ?? 0m);
            decimal lpa = Convert.ToDecimal(lpaRaw ?? 0m);
            string nome = Convert.ToString(nomeRaw ?? "");
            string setor = Convert.ToString(setorRaw ?? "");
            decimal dy = Convert.ToDecimal(dyRaw ?? 0m) * 100;
            long volume = Convert.ToInt64(volumeRaw ?? 0);
            decimal maxDia = Convert.ToDecimal(maxDiaRaw ?? 0m);
            decimal minDia = Convert.ToDecimal(minDiaRaw ?? 0m);
            decimal max52Semanas = Convert.ToDecimal(max52SemanasRaw ?? 0m);
            decimal min52Semanas = Convert.ToDecimal(min52SemanasRaw ?? 0m);
            decimal valorMercado = Convert.ToDecimal(valorMercadoRaw ?? 0m);
            decimal plProjetado = Convert.ToDecimal(plProjetadoRaw ?? 0m);
            decimal pvp = Convert.ToDecimal(pvpRaw ?? 0m);
            decimal abertura = Convert.ToDecimal(aberturaRaw ?? 0m);
            decimal fechamentoAnterior = Convert.ToDecimal(fechamentoRaw ?? 0m);

            return new Recomendacao
            {
                Ticker = ticker.Replace(".SA", ""),
                Nome = nome,
                Setor = setor,
                //TipoAcao = setor,
                PrecoAtual = preco,
                VPA = vpa,
                LPA = lpa,
                Roe = vpa > 0 ? (lpa / vpa) * 100 : 0,
                DividendYield = dy,
                RegularMarketOpen = abertura,
                RegularMarketPreviousClose = fechamentoAnterior,
                RegularMarketDayLow = minDia,
                RegularMarketDayHigh = maxDia,
                FiftyTwoWeekLow = min52Semanas,
                FiftyTwoWeekHigh = max52Semanas,
                ForwardPE = plProjetado,
                PriceToBook = pvp,
                MarketCap = valorMercado,
                RegularMarketVolume = volume,
                DataAtualizacao = DateTime.Now
            };
        }

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