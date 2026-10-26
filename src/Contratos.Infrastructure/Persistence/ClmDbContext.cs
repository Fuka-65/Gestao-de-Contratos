using Contratos.Core.Entities;
using Microsoft.EntityFrameworkCore;

namespace Contratos.Infrastructure.Persistence;

public class ClmDbContext : DbContext
{
    public ClmDbContext(DbContextOptions<ClmDbContext> options) : base(options)
    {
    }

    public DbSet<Usuario> Usuarios => Set<Usuario>();
    public DbSet<Fornecedor> Fornecedores => Set<Fornecedor>();
    public DbSet<Contrato> Contratos => Set<Contrato>();
    public DbSet<TermoAditivo> TermosAditivos => Set<TermoAditivo>();
    public DbSet<FaturaGasto> FaturasGastos => Set<FaturaGasto>();
    public DbSet<RateioDepartamento> RateiosDepartamentos => Set<RateioDepartamento>();
    public DbSet<ConfiguracaoSistema> ConfiguracoesSistema => Set<ConfiguracaoSistema>();
    public DbSet<LogAuditoria> LogsAuditoria => Set<LogAuditoria>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        base.OnModelCreating(modelBuilder);

        modelBuilder.Entity<Usuario>(e =>
        {
            e.ToTable("Usuarios");
            e.HasKey(x => x.Id);
            e.HasIndex(x => x.Login).IsUnique();
            e.HasIndex(x => x.Email).IsUnique();
        });

        modelBuilder.Entity<Fornecedor>(e =>
        {
            e.ToTable("Fornecedores");
            e.HasKey(x => x.Id);
            e.HasIndex(x => x.Cnpj).IsUnique();
        });

        modelBuilder.Entity<Contrato>(e =>
        {
            e.ToTable("Contratos");
            e.HasKey(x => x.Id);
            e.HasIndex(x => x.NumeroContrato).IsUnique();
            e.Property(x => x.ValorMensalContratado).HasPrecision(18, 2);
            e.Property(x => x.ValorGlobalContrato).HasPrecision(18, 2);

            e.HasOne(x => x.Fornecedor)
                .WithMany(f => f.Contratos)
                .HasForeignKey(x => x.FornecedorId)
                .OnDelete(DeleteBehavior.Restrict);
        });

        modelBuilder.Entity<TermoAditivo>(e =>
        {
            e.ToTable("TermosAditivos");
            e.HasKey(x => x.Id);
            e.Property(x => x.NovoValorMensal).HasPrecision(18, 2);

            e.HasOne(x => x.Contrato)
                .WithMany(c => c.TermosAditivos)
                .HasForeignKey(x => x.ContratoId)
                .OnDelete(DeleteBehavior.Cascade);
        });

        modelBuilder.Entity<FaturaGasto>(e =>
        {
            e.ToTable("FaturasGastos");
            e.HasKey(x => x.Id);
            e.Property(x => x.ValorContratado).HasPrecision(18, 2);
            e.Property(x => x.ValorCobrado).HasPrecision(18, 2);
            e.Property(x => x.ValorPago).HasPrecision(18, 2);

            e.HasOne(x => x.Contrato)
                .WithMany(c => c.Faturas)
                .HasForeignKey(x => x.ContratoId)
                .OnDelete(DeleteBehavior.Cascade);

            e.HasOne(x => x.Fornecedor)
                .WithMany(f => f.Faturas)
                .HasForeignKey(x => x.FornecedorId)
                .OnDelete(DeleteBehavior.Restrict);
        });

        modelBuilder.Entity<RateioDepartamento>(e =>
        {
            e.ToTable("RateiosDepartamentos");
            e.HasKey(x => x.Id);
            e.Property(x => x.Percentual).HasPrecision(5, 2);

            e.HasOne(x => x.Contrato)
                .WithMany(c => c.Rateios)
                .HasForeignKey(x => x.ContratoId)
                .OnDelete(DeleteBehavior.Cascade);
        });

        modelBuilder.Entity<ConfiguracaoSistema>(e =>
        {
            e.ToTable("ConfiguracoesSistema");
            e.HasKey(x => x.Id);
        });

        modelBuilder.Entity<LogAuditoria>(e =>
        {
            e.ToTable("LogsAuditoria");
            e.HasKey(x => x.Id);
        });
    }
}
