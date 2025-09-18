using System.ComponentModel.DataAnnotations;
using TransactionsApi.Models.Core;
using TransactionsApi.Models.Data;

public interface ITransactionLogic
{
  void ValidateRequest(string merchantId, CreateTransactionRequest request);
  Transaction ValidateAndEnrich(Transaction transaction);
}

public class TransactionLogic : ITransactionLogic
{
  public void ValidateRequest(string merchantId, CreateTransactionRequest request)
  {
    if (string.IsNullOrWhiteSpace(merchantId))
      throw new ArgumentException("MerchantId is required");

    var validationResults = new List<ValidationResult>();
    var validationContext = new ValidationContext(request);

    if (!Validator.TryValidateObject(request, validationContext, validationResults, true))
    {
      var errors = validationResults.Select(vr => vr.ErrorMessage).ToList();
      throw new ArgumentException($"Validation failed: {string.Join(", ", errors)}");
    }
  }

  public Transaction ValidateAndEnrich(Transaction transaction)
  {
    if (transaction.Amount <= 0)
      throw new ArgumentException("Amount must be greater than zero");

    if (string.IsNullOrWhiteSpace(transaction.MerchantId))
      throw new ArgumentException("MerchantId is required");

    transaction.Id = Guid.NewGuid();
    transaction.DateTime = DateTime.UtcNow;
    transaction.CreatedAt = DateTime.UtcNow;

    return transaction;
  }
}
