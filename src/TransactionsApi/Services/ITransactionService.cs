using TransactionsApi.DTOs;
using TransactionsApi.Models;

namespace TransactionsApi.Services;

public interface ITransactionService
{
    Task<TransactionResponse> CreateTransactionAsync(string merchantId, CreateTransactionRequest request);
    Task<TransactionResponse?> GetTransactionByIdAsync(Guid id);
}