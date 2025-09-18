using TransactionsApi.Models.Core;

namespace TransactionsApi.Models.Data;

public class CreateTransactionEvent
{
    public Guid TransactionId { get; set; }
    public string MerchantId { get; set; } = string.Empty;
    public TransactionType Type { get; set; }
    public decimal Amount { get; set; }
    public DateTime DateTime { get; set; }
    public DateTime CreatedAt { get; set; }
}