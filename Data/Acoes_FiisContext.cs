using Acoes_Fiis.Models;
using Microsoft.EntityFrameworkCore;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;

namespace Acoes_Fiis.Data
{
    public class Acoes_FiisContext : DbContext
    {
        public Acoes_FiisContext(DbContextOptions<Acoes_FiisContext> options)
            : base(options)
        {
        }

        public DbSet<Acoes_Fiis.Models.Recomendacao> Recomendacao { get; set; } = default!;

        public DbSet<Acoes_Fiis.Models.RecomendacaoFii> RecomendacaoFii { get; set; } = default!;

        public DbSet<Acoes_Fiis.Models.AtivoGeral> AtivosGerais { get; set; } = default!;

        public DbSet<Acoes_Fiis.Models.Carteira> Carteira { get; set; } = default!;

        public DbSet<Acoes_Fiis.Models.Financeiro> Financeiro { get; set; } = default!;
        public DbSet<Acoes_Fiis.Models.ContaFixa> ContasFixas { get; set; } = default!;
        public DbSet<Acoes_Fiis.Models.HistoricoAtivo> HistoricoAtivos { get; set; } = default!;
        public DbSet<Acoes_Fiis.Models.Price> Financiamentos { get; set; } = default!;
        public DbSet<Acoes_Fiis.Models.AporteExtra> AporteExtras { get; set; } = default!;
        public DbSet<Acoes_Fiis.Models.FolhaPagamento> FolhasPagamento { get; set; } = default!;
        public DbSet<Acoes_Fiis.Models.Parametro> Parametro { get; set; } = default!;
        public DbSet<Acoes_Fiis.Models.ConfiguracaoBackup> ConfiguracaoBackups { get; set; } = default!;
        public DbSet<Acoes_Fiis.Models.MetaAlocacao> MetasAlocacao { get; set; }
        public DbSet<Acoes_Fiis.Models.EvolucaoPatrimonial> EvolucaoPatrimonial { get; set; }
        public DbSet<Acoes_Fiis.Models.ItemCompra> ItemCompra { get; set; } = default!;
        public DbSet<Acoes_Fiis.Models.ItemCompra> ItensCompras { get; set; }
        public DbSet<Acoes_Fiis.Models.PlanejamentoCompra> PlanejamentoCompras { get; set; }
    }
}
