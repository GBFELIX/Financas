using Acoes_Fiis.Data;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using System;
using System.IO;
using System.Threading;
using System.Threading.Tasks;

public class BackupBackgroundService : BackgroundService
{
    private readonly IServiceScopeFactory _scopeFactory;

    public BackupBackgroundService(IServiceScopeFactory scopeFactory)
    {
        _scopeFactory = scopeFactory;
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        // O serviço roda continuamente enquanto o aplicativo estiver aberto
        while (!stoppingToken.IsCancellationRequested)
        {
            using (var scope = _scopeFactory.CreateScope())
            {
                var context = scope.ServiceProvider.GetRequiredService<Acoes_FiisContext>();
                var config = await context.ConfiguracaoBackups.FirstOrDefaultAsync();

                if (config != null && Directory.Exists(config.CaminhoPastaLocal))
                {
                    // Verifica se já passou o tempo necessário desde o último backup
                    bool precisaDeBackup = !config.UltimoBackup.HasValue ||
                        (DateTime.Now - config.UltimoBackup.Value).TotalHours >= config.IntervaloHoras;

                    if (precisaDeBackup)
                    {
                        try
                        {
                            string nomeBanco = "Investimentos";
                            // Mesmo nome fixo do Controller
                            string nomeArquivoFixo = $"Backup_{nomeBanco}.bak";

                            string pastaTemporaria = @"C:\Users\Public\Documents\BackupsTemp";
                            if (!Directory.Exists(pastaTemporaria)) Directory.CreateDirectory(pastaTemporaria);

                            string caminhoTemporarioSql = Path.Combine(pastaTemporaria, nomeArquivoFixo);
                            string caminhoFinalDrive = Path.Combine(config.CaminhoPastaLocal, nomeArquivoFixo);

                            if (System.IO.File.Exists(caminhoTemporarioSql)) System.IO.File.Delete(caminhoTemporarioSql);

                            // Executa o backup em segundo plano
                            string queryBackup = $"BACKUP DATABASE [{nomeBanco}] TO DISK = '{caminhoTemporarioSql}' WITH FORMAT;";
                            await context.Database.ExecuteSqlRawAsync(queryBackup);

                            // Substitui o arquivo na pasta do Google Drive
                            if (System.IO.File.Exists(caminhoTemporarioSql))
                            {
                                if (System.IO.File.Exists(caminhoFinalDrive))
                                {
                                    System.IO.File.Delete(caminhoFinalDrive);
                                }

                                System.IO.File.Move(caminhoTemporarioSql, caminhoFinalDrive);
                            }

                            config.UltimoBackup = DateTime.Now;
                            await context.SaveChangesAsync();
                        }
                        catch (Exception)
                        {
                            // Falhas silenciosas tratadas
                        }
                    }
                }
            }

            // O serviço "dorme" por 60 minutos antes de checar novamente se precisa rodar o backup (evita consumo de CPU)
            await Task.Delay(TimeSpan.FromMinutes(60), stoppingToken);
        }
    }
}