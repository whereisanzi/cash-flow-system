namespace TransactionsApi.Models;

public enum TransactionType
{
    DEBITO,
    CREDITO
}

public class Transaction
{
    public Guid Id { get; set; }
    public string MerchantId { get; set; } = string.Empty;
    public TransactionType Type { get; set; }
    public decimal Amount { get; set; }
    public DateTime DateTime { get; set; }
    public string? Description { get; set; }
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
}