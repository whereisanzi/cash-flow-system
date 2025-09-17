namespace ConsolidationsApi.Models;

public class DailyConsolidation
{
    public Guid Id { get; set; }
    public string MerchantId { get; set; } = string.Empty;
    public DateOnly Date { get; set; }
    public decimal TotalDebits { get; set; }
    public decimal TotalCredits { get; set; }
    public decimal NetBalance { get; set; }
    public int TransactionCount { get; set; }
    public DateTime LastUpdated { get; set; } = DateTime.UtcNow;
}