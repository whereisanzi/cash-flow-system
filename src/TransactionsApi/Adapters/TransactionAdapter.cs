using TransactionsApi.Models.Core;
using TransactionsApi.Models.Data;

public interface ITransactionAdapter
{
  Transaction ToCore(string merchantId, CreateTransactionRequest request);
  TransactionResponse ToResponse(Transaction transaction);
  CreateTransactionEvent ToEvent(Transaction transaction);
}

public class TransactionAdapter : ITransactionAdapter
{
  public Transaction ToCore(string merchantId, CreateTransactionRequest request)
  {
    return new Transaction
    {
      MerchantId = merchantId,
      Type = request.Type,
      Amount = request.Amount,
      Description = request.Description
    };
  }

  public TransactionResponse ToResponse(Transaction transaction)
  {
    return new TransactionResponse
    {
      Id = transaction.Id,
      MerchantId = transaction.MerchantId,
      Type = transaction.Type,
      Amount = transaction.Amount,
      DateTime = transaction.DateTime,
      Description = transaction.Description,
      CreatedAt = transaction.CreatedAt
    };
  }

  public CreateTransactionEvent ToEvent(Transaction transaction)
  {
    return new CreateTransactionEvent
    {
      TransactionId = transaction.Id,
      MerchantId = transaction.MerchantId,
      Type = transaction.Type,
      Amount = transaction.Amount,
      DateTime = transaction.DateTime,
      CreatedAt = transaction.CreatedAt
    };
  }
}