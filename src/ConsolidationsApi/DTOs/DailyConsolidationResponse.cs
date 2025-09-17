namespace ConsolidationsApi.DTOs;

public class DailyConsolidationResponse
{
    public string MerchantId { get; set; } = string.Empty;
    public DateOnly Date { get; set; }
    public decimal TotalDebits { get; set; }
    public decimal TotalCredits { get; set; }
    public decimal NetBalance { get; set; }
    public int TransactionCount { get; set; }
    public DateTime LastUpdated { get; set; }
}