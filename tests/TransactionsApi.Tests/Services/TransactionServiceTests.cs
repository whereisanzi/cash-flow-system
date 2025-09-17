using Moq;
using Microsoft.Extensions.Logging;
using TransactionsApi.DTOs;
using TransactionsApi.Events;
using TransactionsApi.Models;
using TransactionsApi.Repositories;
using TransactionsApi.Services;
using Xunit;

namespace TransactionsApi.Tests.Services;

public class TransactionServiceTests
{
    private readonly Mock<ITransactionRepository> _mockRepository;
    private readonly Mock<IEventPublisher> _mockEventPublisher;
    private readonly Mock<ILogger<TransactionService>> _mockLogger;
    private readonly TransactionService _service;

    public TransactionServiceTests()
    {
        _mockRepository = new Mock<ITransactionRepository>();
        _mockEventPublisher = new Mock<IEventPublisher>();
        _mockLogger = new Mock<ILogger<TransactionService>>();
        _service = new TransactionService(_mockRepository.Object, _mockEventPublisher.Object, _mockLogger.Object);
    }

    [Fact]
    public async Task CreateTransactionAsync_ShouldCreateTransactionAndPublishEvent()
    {
        // Arrange
        var merchantId = "merchant-123";
        var request = new CreateTransactionRequest
        {
            Type = TransactionType.CREDITO,
            Amount = 100.50m,
            Description = "Test transaction"
        };

        var expectedTransaction = new Transaction
        {
            Id = Guid.NewGuid(),
            MerchantId = merchantId,
            Type = request.Type,
            Amount = request.Amount,
            Description = request.Description
        };

        _mockRepository.Setup(r => r.CreateAsync(It.IsAny<Transaction>()))
            .ReturnsAsync(expectedTransaction);

        // Act
        var result = await _service.CreateTransactionAsync(merchantId, request);

        // Assert
        Assert.Equal(expectedTransaction.Id, result.Id);
        Assert.Equal(merchantId, result.MerchantId);
        Assert.Equal(request.Type, result.Type);
        Assert.Equal(request.Amount, result.Amount);
        Assert.Equal(request.Description, result.Description);

        _mockRepository.Verify(r => r.CreateAsync(It.IsAny<Transaction>()), Times.Once);
        _mockEventPublisher.Verify(p => p.PublishAsync(It.IsAny<TransactionCreatedEvent>(), "transaction.created"), Times.Once);
    }

    [Theory]
    [InlineData(TransactionType.DEBITO, 50.25)]
    [InlineData(TransactionType.CREDITO, 75.75)]
    public async Task CreateTransactionAsync_ShouldHandleDifferentTransactionTypes(TransactionType type, decimal amount)
    {
        // Arrange
        var merchantId = "merchant-456";
        var request = new CreateTransactionRequest
        {
            Type = type,
            Amount = amount
        };

        var expectedTransaction = new Transaction
        {
            Id = Guid.NewGuid(),
            MerchantId = merchantId,
            Type = type,
            Amount = amount
        };

        _mockRepository.Setup(r => r.CreateAsync(It.IsAny<Transaction>()))
            .ReturnsAsync(expectedTransaction);

        // Act
        var result = await _service.CreateTransactionAsync(merchantId, request);

        // Assert
        Assert.Equal(type, result.Type);
        Assert.Equal(amount, result.Amount);
    }

    [Fact]
    public async Task GetTransactionByIdAsync_ShouldReturnTransactionWhenExists()
    {
        // Arrange
        var transactionId = Guid.NewGuid();
        var expectedTransaction = new Transaction
        {
            Id = transactionId,
            MerchantId = "merchant-789",
            Type = TransactionType.CREDITO,
            Amount = 200m
        };

        _mockRepository.Setup(r => r.GetByIdAsync(transactionId))
            .ReturnsAsync(expectedTransaction);

        // Act
        var result = await _service.GetTransactionByIdAsync(transactionId);

        // Assert
        Assert.NotNull(result);
        Assert.Equal(transactionId, result.Id);
        Assert.Equal(expectedTransaction.MerchantId, result.MerchantId);
    }

    [Fact]
    public async Task GetTransactionByIdAsync_ShouldReturnNullWhenNotExists()
    {
        // Arrange
        var transactionId = Guid.NewGuid();
        _mockRepository.Setup(r => r.GetByIdAsync(transactionId))
            .ReturnsAsync((Transaction?)null);

        // Act
        var result = await _service.GetTransactionByIdAsync(transactionId);

        // Assert
        Assert.Null(result);
    }
}