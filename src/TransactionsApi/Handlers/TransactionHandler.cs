using TransactionsApi.Models.Data;

public static class TransactionHandler
{
  public static async Task<IResult> CreateTransaction(
    string merchantId,
    CreateTransactionRequest request,
    IDatabaseGateway databaseGateway,
    IQueueGateway queueGateway,
    ITransactionAdapter transactionAdapter,
    ITransactionLogic transactionLogic)
  {
    try
    {
      var result = await TransactionFlow.CreateTransaction(merchantId, request, databaseGateway, queueGateway, transactionAdapter, transactionLogic);
      return Results.Created($"/api/v1/transactions/{result.Id}", result);
    }
    catch (ArgumentException ex)
    {
      return Results.BadRequest(ex.Message);
    }
    catch (Exception ex)
    {
      return Results.Problem($"An error occurred: {ex.Message}");
    }
  }

  public static IResult HealthCheck()
  {
    return Results.Ok(new { Status = "Healthy", Service = "TransactionsApi" });
  }
}
