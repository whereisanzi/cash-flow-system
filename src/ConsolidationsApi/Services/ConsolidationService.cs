using ConsolidationsApi.DTOs;
using ConsolidationsApi.Models;
using ConsolidationsApi.Repositories;

namespace ConsolidationsApi.Services;

public class ConsolidationService : IConsolidationService
{
    private readonly IDailyConsolidationRepository _repository;
    private readonly ILogger<ConsolidationService> _logger;

    public ConsolidationService(IDailyConsolidationRepository repository, ILogger<ConsolidationService> logger)
    {
        _repository = repository;
        _logger = logger;
    }

    public async Task<DailyConsolidationResponse?> GetDailyConsolidationAsync(string merchantId, DateOnly date)
    {
        var consolidation = await _repository.GetByMerchantAndDateAsync(merchantId, date);
        if (consolidation == null)
            return null;

        return new DailyConsolidationResponse
        {
            MerchantId = consolidation.MerchantId,
            Date = consolidation.Date,
            TotalDebits = consolidation.TotalDebits,
            TotalCredits = consolidation.TotalCredits,
            NetBalance = consolidation.NetBalance,
            TransactionCount = consolidation.TransactionCount,
            LastUpdated = consolidation.LastUpdated
        };
    }

    public async Task UpdateConsolidationFromTransactionAsync(string merchantId, TransactionType transactionType, decimal amount, DateTime transactionDate)
    {
        var date = DateOnly.FromDateTime(transactionDate);
        var existing = await _repository.GetByMerchantAndDateAsync(merchantId, date);

        var consolidation = existing ?? new DailyConsolidation
        {
            Id = Guid.NewGuid(),
            MerchantId = merchantId,
            Date = date,
            TotalDebits = 0,
            TotalCredits = 0,
            NetBalance = 0,
            TransactionCount = 0
        };

        if (transactionType == TransactionType.DEBITO)
        {
            consolidation.TotalDebits += amount;
        }
        else if (transactionType == TransactionType.CREDITO)
        {
            consolidation.TotalCredits += amount;
        }

        consolidation.NetBalance = consolidation.TotalCredits - consolidation.TotalDebits;
        consolidation.TransactionCount++;
        consolidation.LastUpdated = DateTime.UtcNow;

        await _repository.CreateOrUpdateAsync(consolidation);

        _logger.LogInformation("Updated consolidation for merchant {MerchantId} on {Date}. New balance: {NetBalance}",
            merchantId, date, consolidation.NetBalance);
    }
}