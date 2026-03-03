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

        protected override void OnModelCreating(ModelBuilder modelBuilder)
        {
            // Configuração para garantir que o Decimal não perca os centavos no banco
            modelBuilder.Entity<RecomendacaoFii>()
                .Property(r => r.PrecoAtual).HasPrecision(18, 2);
            modelBuilder.Entity<RecomendacaoFii>()
                .Property(r => r.VPA).HasPrecision(18, 2);
            modelBuilder.Entity<RecomendacaoFii>()
                .Property(r => r.UltimoRendimento).HasPrecision(18, 2);
            modelBuilder.Entity<RecomendacaoFii>()
                .Property(r => r.Vacancia).HasPrecision(18, 2);

            modelBuilder.Entity<Carteira>().ToTable("Carteira");

            modelBuilder.Entity<Acoes_Fiis.Models.Carteira>().ToTable("Carteira");



            modelBuilder.Entity<Carteira>().Property(c => c.PrecoMedio).HasPrecision(18, 2); //4

            base.OnModelCreating(modelBuilder);
        }
    }
}
