using TransactionsApi.DTOs;
using TransactionsApi.Events;
using TransactionsApi.Models;
using TransactionsApi.Repositories;

namespace TransactionsApi.Services;

public class TransactionService : ITransactionService
{
    private readonly ITransactionRepository _repository;
    private readonly IEventPublisher _eventPublisher;
    private readonly ILogger<TransactionService> _logger;

    public TransactionService(
        ITransactionRepository repository,
        IEventPublisher eventPublisher,
        ILogger<TransactionService> logger)
    {
        _repository = repository;
        _eventPublisher = eventPublisher;
        _logger = logger;
    }

    public async Task<TransactionResponse> CreateTransactionAsync(string merchantId, CreateTransactionRequest request)
    {
        var transaction = new Transaction
        {
            Id = Guid.NewGuid(),
            MerchantId = merchantId,
            Type = request.Type,
            Amount = request.Amount,
            DateTime = DateTime.UtcNow,
            Description = request.Description,
            CreatedAt = DateTime.UtcNow
        };

        var createdTransaction = await _repository.CreateAsync(transaction);

        var transactionEvent = new TransactionCreatedEvent
        {
            TransactionId = createdTransaction.Id,
            MerchantId = createdTransaction.MerchantId,
            Type = createdTransaction.Type,
            Amount = createdTransaction.Amount,
            DateTime = createdTransaction.DateTime,
            CreatedAt = createdTransaction.CreatedAt
        };

        try
        {
            await _eventPublisher.PublishAsync(transactionEvent, "transaction.created");
            _logger.LogInformation("Transaction {TransactionId} created and event published for merchant {MerchantId}",
                createdTransaction.Id, merchantId);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to publish event for transaction {TransactionId}", createdTransaction.Id);
        }

        return new TransactionResponse
        {
            Id = createdTransaction.Id,
            MerchantId = createdTransaction.MerchantId,
            Type = createdTransaction.Type,
            Amount = createdTransaction.Amount,
            DateTime = createdTransaction.DateTime,
            Description = createdTransaction.Description,
            CreatedAt = createdTransaction.CreatedAt
        };
    }

    public async Task<TransactionResponse?> GetTransactionByIdAsync(Guid id)
    {
        var transaction = await _repository.GetByIdAsync(id);
        if (transaction == null)
            return null;

        return new TransactionResponse
        {
            Id = transaction.Id,
            MerchantId = transaction.MerchantId,
            Type = transaction.Type,
            Amount = transaction.Amount,
            DateTime = transaction.DateTime,
            Description = transaction.Description,
            CreatedAt = transaction.CreatedAt
        };
    }
}