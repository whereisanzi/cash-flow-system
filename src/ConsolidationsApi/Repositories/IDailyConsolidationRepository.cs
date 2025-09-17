using ConsolidationsApi.Models;

namespace ConsolidationsApi.Repositories;

public interface IDailyConsolidationRepository
{
    Task<DailyConsolidation?> GetByMerchantAndDateAsync(string merchantId, DateOnly date);
    Task<DailyConsolidation> CreateOrUpdateAsync(DailyConsolidation consolidation);
    Task<IEnumerable<DailyConsolidation>> GetByMerchantIdAsync(string merchantId, DateOnly? startDate = null, DateOnly? endDate = null);
}