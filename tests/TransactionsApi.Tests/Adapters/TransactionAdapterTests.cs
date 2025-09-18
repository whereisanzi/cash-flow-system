using TransactionsApi.Models.Core;
using TransactionsApi.Models.Data;
using Xunit;

namespace TransactionsApi.Tests.Adapters;

public class TransactionAdapterTests
{
    private readonly TransactionAdapter _adapter = new();

    [Fact]
    public void ToCore_ShouldConvertCreateTransactionRequestToTransaction()
    {
        // Arrange
        var merchantId = "merchant-123";
        var request = new CreateTransactionRequest
        {
            Type = TransactionType.CREDITO,
            Amount = 100.50m,
            Description = "Test transaction"
        };

        // Act
        var result = _adapter.ToCore(merchantId, request);

        // Assert
        Assert.Equal(merchantId, result.MerchantId);
        Assert.Equal(request.Type, result.Type);
        Assert.Equal(request.Amount, result.Amount);
        Assert.Equal(request.Description, result.Description);
        Assert.Equal(Guid.Empty, result.Id); // Should be empty before enrichment
        Assert.Equal(default(DateTime), result.DateTime); // Should be default before enrichment
        Assert.True(result.CreatedAt > DateTime.MinValue); // CreatedAt has default value of DateTime.UtcNow
    }

    [Fact]
    public void ToResponse_ShouldConvertTransactionToTransactionResponse()
    {
        // Arrange
        var transaction = new Transaction
        {
            Id = Guid.NewGuid(),
            MerchantId = "merchant-456",
            Type = TransactionType.DEBITO,
            Amount = 75.25m,
            DateTime = DateTime.UtcNow,
            Description = "Test response",
            CreatedAt = DateTime.UtcNow
        };

        // Act
        var result = _adapter.ToResponse(transaction);

        // Assert
        Assert.Equal(transaction.Id, result.Id);
        Assert.Equal(transaction.MerchantId, result.MerchantId);
        Assert.Equal(transaction.Type, result.Type);
        Assert.Equal(transaction.Amount, result.Amount);
        Assert.Equal(transaction.DateTime, result.DateTime);
        Assert.Equal(transaction.Description, result.Description);
        Assert.Equal(transaction.CreatedAt, result.CreatedAt);
    }

    [Fact]
    public void ToEvent_ShouldConvertTransactionToCreateTransactionEvent()
    {
        // Arrange
        var transaction = new Transaction
        {
            Id = Guid.NewGuid(),
            MerchantId = "merchant-789",
            Type = TransactionType.CREDITO,
            Amount = 200m,
            DateTime = DateTime.UtcNow,
            Description = "Test event",
            CreatedAt = DateTime.UtcNow
        };

        // Act
        var result = _adapter.ToEvent(transaction);

        // Assert
        Assert.Equal(transaction.Id, result.TransactionId);
        Assert.Equal(transaction.MerchantId, result.MerchantId);
        Assert.Equal(transaction.Type, result.Type);
        Assert.Equal(transaction.Amount, result.Amount);
        Assert.Equal(transaction.DateTime, result.DateTime);
        Assert.Equal(transaction.CreatedAt, result.CreatedAt);
    }

    [Theory]
    [InlineData(TransactionType.DEBITO, 50.25)]
    [InlineData(TransactionType.CREDITO, 150.75)]
    public void ToCore_ShouldHandleDifferentTransactionTypes(TransactionType type, decimal amount)
    {
        // Arrange
        var merchantId = "merchant-test";
        var request = new CreateTransactionRequest
        {
            Type = type,
            Amount = amount
        };

        // Act
        var result = _adapter.ToCore(merchantId, request);

        // Assert
        Assert.Equal(type, result.Type);
        Assert.Equal(amount, result.Amount);
    }

    [Fact]
    public void ToCore_ShouldHandleNullDescription()
    {
        // Arrange
        var merchantId = "merchant-null";
        var request = new CreateTransactionRequest
        {
            Type = TransactionType.CREDITO,
            Amount = 100m,
            Description = null
        };

        // Act
        var result = _adapter.ToCore(merchantId, request);

        // Assert
        Assert.Null(result.Description);
    }
}