using Microsoft.EntityFrameworkCore;
using ConsolidationsApi.Data;
using ConsolidationsApi.Models;

namespace ConsolidationsApi.Repositories;

public class DailyConsolidationRepository : IDailyConsolidationRepository
{
    private readonly ConsolidationsDbContext _context;

    public DailyConsolidationRepository(ConsolidationsDbContext context)
    {
        _context = context;
    }

    public async Task<DailyConsolidation?> GetByMerchantAndDateAsync(string merchantId, DateOnly date)
    {
        return await _context.DailyConsolidations
            .FirstOrDefaultAsync(c => c.MerchantId == merchantId && c.Date == date);
    }

    public async Task<DailyConsolidation> CreateOrUpdateAsync(DailyConsolidation consolidation)
    {
        var existing = await GetByMerchantAndDateAsync(consolidation.MerchantId, consolidation.Date);

        if (existing == null)
        {
            _context.DailyConsolidations.Add(consolidation);
        }
        else
        {
            existing.TotalDebits = consolidation.TotalDebits;
            existing.TotalCredits = consolidation.TotalCredits;
            existing.NetBalance = consolidation.NetBalance;
            existing.TransactionCount = consolidation.TransactionCount;
            existing.LastUpdated = DateTime.UtcNow;
        }

        await _context.SaveChangesAsync();
        return existing ?? consolidation;
    }

    public async Task<IEnumerable<DailyConsolidation>> GetByMerchantIdAsync(string merchantId, DateOnly? startDate = null, DateOnly? endDate = null)
    {
        var query = _context.DailyConsolidations
            .Where(c => c.MerchantId == merchantId);

        if (startDate.HasValue)
            query = query.Where(c => c.Date >= startDate.Value);

        if (endDate.HasValue)
            query = query.Where(c => c.Date <= endDate.Value);

        return await query
            .OrderByDescending(c => c.Date)
            .ToListAsync();
    }
}