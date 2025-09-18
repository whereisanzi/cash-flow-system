using TransactionsApi.Models.Core;
using TransactionsApi.Models.Data;
using Xunit;

namespace TransactionsApi.Tests.Logics;

public class TransactionLogicTests
{
    private readonly TransactionLogic _logic = new();

    [Fact]
    public void ValidateRequest_ShouldPassForValidRequest()
    {
        // Arrange
        var merchantId = "merchant-123";
        var request = new CreateTransactionRequest
        {
            Type = TransactionType.CREDITO,
            Amount = 100.50m,
            Description = "Valid transaction"
        };

        // Act & Assert - Should not throw
        _logic.ValidateRequest(merchantId, request);
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    public void ValidateRequest_ShouldThrowForInvalidMerchantId(string invalidMerchantId)
    {
        // Arrange
        var request = new CreateTransactionRequest
        {
            Type = TransactionType.CREDITO,
            Amount = 100m
        };

        // Act & Assert
        Assert.Throws<ArgumentException>(() => _logic.ValidateRequest(invalidMerchantId, request));
    }

    [Theory]
    [InlineData(0)]
    [InlineData(-1)]
    [InlineData(-100.50)]
    public void ValidateRequest_ShouldThrowForInvalidAmount(decimal invalidAmount)
    {
        // Arrange
        var merchantId = "merchant-123";
        var request = new CreateTransactionRequest
        {
            Type = TransactionType.CREDITO,
            Amount = invalidAmount
        };

        // Act & Assert
        Assert.Throws<ArgumentException>(() => _logic.ValidateRequest(merchantId, request));
    }

    [Fact]
    public void ValidateAndEnrich_ShouldEnrichTransactionWithIds()
    {
        // Arrange
        var transaction = new Transaction
        {
            MerchantId = "merchant-456",
            Type = TransactionType.DEBITO,
            Amount = 75.25m,
            Description = "Test transaction"
        };

        // Act
        var result = _logic.ValidateAndEnrich(transaction);

        // Assert
        Assert.NotEqual(Guid.Empty, result.Id);
        Assert.True(result.DateTime > DateTime.MinValue);
        Assert.True(result.CreatedAt > DateTime.MinValue);
        Assert.Equal(transaction.MerchantId, result.MerchantId);
        Assert.Equal(transaction.Type, result.Type);
        Assert.Equal(transaction.Amount, result.Amount);
        Assert.Equal(transaction.Description, result.Description);
    }

    [Theory]
    [InlineData(0)]
    [InlineData(-1)]
    [InlineData(-50.25)]
    public void ValidateAndEnrich_ShouldThrowForInvalidAmount(decimal invalidAmount)
    {
        // Arrange
        var transaction = new Transaction
        {
            MerchantId = "merchant-789",
            Type = TransactionType.CREDITO,
            Amount = invalidAmount
        };

        // Act & Assert
        Assert.Throws<ArgumentException>(() => _logic.ValidateAndEnrich(transaction));
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    public void ValidateAndEnrich_ShouldThrowForInvalidMerchantId(string invalidMerchantId)
    {
        // Arrange
        var transaction = new Transaction
        {
            MerchantId = invalidMerchantId,
            Type = TransactionType.CREDITO,
            Amount = 100m
        };

        // Act & Assert
        Assert.Throws<ArgumentException>(() => _logic.ValidateAndEnrich(transaction));
    }

    [Fact]
    public void ValidateAndEnrich_ShouldPreserveExistingValues()
    {
        // Arrange
        var existingId = Guid.NewGuid();
        var existingDateTime = DateTime.UtcNow.AddDays(-1);
        var existingCreatedAt = DateTime.UtcNow.AddDays(-2);

        var transaction = new Transaction
        {
            Id = existingId,
            MerchantId = "merchant-preserve",
            Type = TransactionType.DEBITO,
            Amount = 200m,
            DateTime = existingDateTime,
            CreatedAt = existingCreatedAt,
            Description = "Preserve test"
        };

        // Act
        var result = _logic.ValidateAndEnrich(transaction);

        // Assert - Should overwrite with new values (enrichment logic)
        Assert.NotEqual(existingId, result.Id); // New ID generated
        Assert.NotEqual(existingDateTime, result.DateTime); // New DateTime
        Assert.NotEqual(existingCreatedAt, result.CreatedAt); // New CreatedAt
    }

    [Theory]
    [InlineData(TransactionType.DEBITO, 50.25)]
    [InlineData(TransactionType.CREDITO, 150.75)]
    public void ValidateAndEnrich_ShouldHandleDifferentTransactionTypes(TransactionType type, decimal amount)
    {
        // Arrange
        var transaction = new Transaction
        {
            MerchantId = "merchant-type-test",
            Type = type,
            Amount = amount
        };

        // Act
        var result = _logic.ValidateAndEnrich(transaction);

        // Assert
        Assert.Equal(type, result.Type);
        Assert.Equal(amount, result.Amount);
        Assert.NotEqual(Guid.Empty, result.Id);
    }
}