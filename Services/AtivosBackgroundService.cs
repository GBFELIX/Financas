using Acoes_Fiis.Data;
using Acoes_Fiis.Models;
using Acoes_Fiis.Services;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;

public class AtivosBackgroundService : BackgroundService
{
    private readonly IServiceScopeFactory _scopeFactory;
    private readonly TimeSpan _intervaloExecucao = TimeSpan.FromHours(1);

    public AtivosBackgroundService(IServiceScopeFactory scopeFactory)
    {
        _scopeFactory = scopeFactory;
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        while (!stoppingToken.IsCancellationRequested)
        {
            try
            {
                using (var scope = _scopeFactory.CreateScope())
                {
                    var context = scope.ServiceProvider.GetRequiredService<Acoes_FiisContext>();
                    var service = new YahooService();

                    // --- 1. ATUALIZAÇÃO DAS AÇÕES ---
                    var acoes = await context.Recomendacao.ToListAsync(stoppingToken);
                    foreach (var item in acoes)
                    {
                        if (item.DataAtualizacao > DateTime.Now.AddMinutes(-1440)) continue;

                        try
                        {
                            var dados = await service.ObterDadosAtivo(item.Ticker);
                            item.Nome = dados.Nome;
                            item.Setor = dados.Setor;
                            //item.TipoAcao = dados.TipoAcao;
                            item.PrecoAtual = dados.PrecoAtual;
                            item.VPA = dados.VPA;
                            item.LPA = dados.LPA;
                            item.Roe = dados.Roe;
                            item.DividendYield = dados.DividendYield;

                            item.RegularMarketOpen = dados.RegularMarketOpen;
                            item.RegularMarketPreviousClose = dados.RegularMarketPreviousClose;
                            item.RegularMarketDayLow = dados.RegularMarketDayLow;
                            item.RegularMarketDayHigh = dados.RegularMarketDayHigh;
                            item.FiftyTwoWeekLow = dados.FiftyTwoWeekLow;
                            item.FiftyTwoWeekHigh = dados.FiftyTwoWeekHigh;
                            item.ForwardPE = dados.ForwardPE;
                            item.PriceToBook = dados.PriceToBook;
                            item.MarketCap = dados.MarketCap;
                            item.RegularMarketVolume = dados.RegularMarketVolume;

                            item.DataAtualizacao = DateTime.Now;

                            context.Update(item);
                        }
                        catch { continue; }
                    }

                    // --- 2. ATUALIZAÇÃO DOS FIIS ---
                    var fiis = await context.RecomendacaoFii.ToListAsync(stoppingToken);
                    foreach (var item in fiis)
                    {
                        if (item.DataAtualizacao > DateTime.Now.AddMinutes(-1440)) continue;

                        try
                        {
                            // Garante o sufixo .SA exigido pelo Yahoo Finance para ativos brasileiros
                            string tickerFormatado = item.Ticker.EndsWith(".SA") ? item.Ticker : item.Ticker + ".SA";
                            var dados = await service.ObterDadosAtivo(tickerFormatado);

                            item.PrecoAtual = dados.PrecoAtual;
                            item.VPA = dados.VPA;
                            item.DataAtualizacao = DateTime.Now;

                            context.Update(item);
                        }
                        catch { continue; }
                    }

                    var outros = await context.AtivosGerais.ToListAsync(stoppingToken);

                    decimal cotacaoDolar = 1.00m;
                    try
                    {
                        var dolarDados = await service.ObterDadosAtivo("USDBRL=X");
                        if (dolarDados != null && dolarDados.PrecoAtual > 0)
                        {
                            cotacaoDolar = dolarDados.PrecoAtual;
                        }
                    }
                    catch
                    {
                        cotacaoDolar = 5.20m;
                    }

                    foreach (var item in outros)
                    {
                        if (item.DataAtualizacao > DateTime.Now.AddMinutes(-1440)) continue;

                        try
                        {
                            string tickerFormatado = item.Ticker.Trim();

                            if (item.Moeda != "USD" && !tickerFormatado.Contains("-") && !tickerFormatado.EndsWith(".SA"))
                            {
                                tickerFormatado += ".SA";
                            }

                            var dados = await service.ObterDadosAtivo(tickerFormatado);

                            if (dados != null && dados.PrecoAtual > 0)
                            {
                                // Se o ativo for negociado em dólar (como o BTC-USD ou ações americanas), multiplica pelo câmbio do dia
                                if (item.Moeda == "USD" || tickerFormatado.EndsWith("-USD"))
                                {
                                    item.PrecoAtual = Math.Round(dados.PrecoAtual * cotacaoDolar, 2);
                                }
                                else
                                {
                                    item.PrecoAtual = Math.Round(dados.PrecoAtual, 2);
                                }

                                item.DataAtualizacao = DateTime.Now;

                                context.Entry(item).State = EntityState.Modified;
                            }
                        }
                        catch
                        {
                            continue;
                        }
                    }

                    // Salva em lote todas as cotações atualizadas e convertidas com segurança
                    await context.SaveChangesAsync(stoppingToken);
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Erro na rotina automática de ativos: {ex.Message}");
            }

            await Task.Delay(_intervaloExecucao, stoppingToken);
        }
    }
}