using TransactionsApi.Models.Data;
using TransactionsApi.Models.Core;

public static class TransactionFlow
{
  public static async Task<TransactionResponse> CreateTransaction(
    string merchantId,
    CreateTransactionRequest request,
    IDatabaseGateway databaseGateway,
    IQueueGateway queueGateway,
    ITransactionAdapter transactionAdapter,
    ITransactionLogic transactionLogic)
  {
    transactionLogic.ValidateRequest(merchantId, request);

    var transaction = transactionAdapter.ToCore(merchantId, request);

    var validatedTransaction = transactionLogic.ValidateAndEnrich(transaction);

    var savedTransaction = await databaseGateway.SaveTransactionAsync(validatedTransaction);

    await queueGateway.PublishTransactionCreatedAsync(savedTransaction);

    return transactionAdapter.ToResponse(savedTransaction);
  }
}
