using System.ComponentModel.DataAnnotations;
using TransactionsApi.Models.Core;

namespace TransactionsApi.Models.Data;

public class CreateTransactionRequest
{
    [Required]
    public TransactionType Type { get; set; }

    [Required]
    [Range(0.01, double.MaxValue, ErrorMessage = "Amount must be greater than 0")]
    public decimal Amount { get; set; }

    public string? Description { get; set; }
}
