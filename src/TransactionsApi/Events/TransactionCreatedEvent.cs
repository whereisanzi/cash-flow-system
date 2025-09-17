using TransactionsApi.Models;

namespace TransactionsApi.Events;

public class TransactionCreatedEvent
{
    public Guid TransactionId { get; set; }
    public string MerchantId { get; set; } = string.Empty;
    public TransactionType Type { get; set; }
    public decimal Amount { get; set; }
    public DateTime DateTime { get; set; }
    public DateTime CreatedAt { get; set; }
}