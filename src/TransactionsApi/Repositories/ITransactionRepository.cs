using TransactionsApi.Models;

namespace TransactionsApi.Repositories;

public interface ITransactionRepository
{
    Task<Transaction> CreateAsync(Transaction transaction);
    Task<Transaction?> GetByIdAsync(Guid id);
    Task<IEnumerable<Transaction>> GetByMerchantIdAsync(string merchantId, DateTime? startDate = null, DateTime? endDate = null);
}