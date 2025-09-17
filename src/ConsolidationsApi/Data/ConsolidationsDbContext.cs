using Microsoft.EntityFrameworkCore;
using ConsolidationsApi.Models;

namespace ConsolidationsApi.Data;

public class ConsolidationsDbContext : DbContext
{
    public ConsolidationsDbContext(DbContextOptions<ConsolidationsDbContext> options) : base(options)
    {
    }

    public DbSet<DailyConsolidation> DailyConsolidations { get; set; } = null!;

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        modelBuilder.Entity<DailyConsolidation>(entity =>
        {
            entity.HasKey(e => e.Id);
            entity.Property(e => e.MerchantId).IsRequired().HasMaxLength(100);
            entity.Property(e => e.Date).IsRequired();
            entity.Property(e => e.TotalDebits).IsRequired().HasColumnType("decimal(18,2)");
            entity.Property(e => e.TotalCredits).IsRequired().HasColumnType("decimal(18,2)");
            entity.Property(e => e.NetBalance).IsRequired().HasColumnType("decimal(18,2)");
            entity.Property(e => e.TransactionCount).IsRequired();
            entity.Property(e => e.LastUpdated).IsRequired();

            entity.HasIndex(e => new { e.MerchantId, e.Date }).IsUnique();
        });
    }
}