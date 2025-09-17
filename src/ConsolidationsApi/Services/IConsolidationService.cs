using ConsolidationsApi.DTOs;
using ConsolidationsApi.Models;

namespace ConsolidationsApi.Services;

public interface IConsolidationService
{
    Task<DailyConsolidationResponse?> GetDailyConsolidationAsync(string merchantId, DateOnly date);
    Task UpdateConsolidationFromTransactionAsync(string merchantId, TransactionType transactionType, decimal amount, DateTime transactionDate);
}