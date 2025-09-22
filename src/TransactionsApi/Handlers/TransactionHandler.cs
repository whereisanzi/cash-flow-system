using TransactionsApi.Models.Data;
using System.Diagnostics;
using Serilog;

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
    var stopwatch = Stopwatch.StartNew();

    Log.Information("Starting transaction creation for {MerchantId} with type {TransactionType} and amount {Amount}",
      merchantId, request.Type, request.Amount);

    try
    {
      var result = await TransactionFlow.CreateTransaction(merchantId, request, databaseGateway, queueGateway, transactionAdapter, transactionLogic);

      Log.Information("Transaction created successfully for {MerchantId} with ID {TransactionId} in {Duration}ms",
        merchantId, result.Id, stopwatch.ElapsedMilliseconds);

      return Results.Created($"/api/v1/transactions/{result.Id}", result);
    }
    catch (ArgumentException ex)
    {
      Log.Warning("Transaction validation failed for {MerchantId}: {ValidationError}", merchantId, ex.Message);
      return Results.BadRequest(ex.Message);
    }
    catch (Exception ex)
    {
      Log.Error(ex, "Internal error occurred while creating transaction for {MerchantId}", merchantId);
      return Results.Problem($"An error occurred: {ex.Message}");
    }
    finally
    {
      stopwatch.Stop();
    }
  }

  public static IResult HealthCheck()
  {
    return Results.Ok(new { Status = "Healthy", Service = "TransactionsApi" });
  }
}
