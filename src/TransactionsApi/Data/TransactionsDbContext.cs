using Microsoft.EntityFrameworkCore;
using TransactionsApi.Models;

namespace TransactionsApi.Data;

public class TransactionsDbContext : DbContext
{
    public TransactionsDbContext(DbContextOptions<TransactionsDbContext> options) : base(options)
    {
    }

    public DbSet<Transaction> Transactions { get; set; } = null!;

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        modelBuilder.Entity<Transaction>(entity =>
        {
            entity.HasKey(e => e.Id);
            entity.Property(e => e.MerchantId).IsRequired().HasMaxLength(100);
            entity.Property(e => e.Type).IsRequired().HasConversion<string>();
            entity.Property(e => e.Amount).IsRequired().HasColumnType("decimal(18,2)");
            entity.Property(e => e.DateTime).IsRequired();
            entity.Property(e => e.Description).HasMaxLength(500);
            entity.Property(e => e.CreatedAt).IsRequired();

            entity.HasIndex(e => e.MerchantId);
            entity.HasIndex(e => e.DateTime);
        });
    }
}