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
                            item.PrecoAtual = dados.PrecoAtual;
                            item.VPA = dados.VPA;
                            item.LPA = dados.LPA;
                            item.Roe = dados.Roe;
                            item.DividendYield = dados.DividendYield;
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

                    // Salva todas as alterações de ambas as tabelas de uma só vez
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