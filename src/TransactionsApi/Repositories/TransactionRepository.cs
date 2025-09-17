using Microsoft.EntityFrameworkCore;
using TransactionsApi.Data;
using TransactionsApi.Models;

namespace TransactionsApi.Repositories;

public class TransactionRepository : ITransactionRepository
{
    private readonly TransactionsDbContext _context;

    public TransactionRepository(TransactionsDbContext context)
    {
        _context = context;
    }

    public async Task<Transaction> CreateAsync(Transaction transaction)
    {
        _context.Transactions.Add(transaction);
        await _context.SaveChangesAsync();
        return transaction;
    }

    public async Task<Transaction?> GetByIdAsync(Guid id)
    {
        return await _context.Transactions
            .FirstOrDefaultAsync(t => t.Id == id);
    }

    public async Task<IEnumerable<Transaction>> GetByMerchantIdAsync(string merchantId, DateTime? startDate = null, DateTime? endDate = null)
    {
        var query = _context.Transactions
            .Where(t => t.MerchantId == merchantId);

        if (startDate.HasValue)
            query = query.Where(t => t.DateTime >= startDate.Value);

        if (endDate.HasValue)
            query = query.Where(t => t.DateTime <= endDate.Value);

        return await query
            .OrderByDescending(t => t.DateTime)
            .ToListAsync();
    }
}