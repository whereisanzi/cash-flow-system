using Moq;
using Microsoft.Extensions.Logging;
using ConsolidationsApi.Models;
using ConsolidationsApi.Repositories;
using ConsolidationsApi.Services;
using Xunit;

namespace ConsolidationsApi.Tests.Services;

public class ConsolidationServiceTests
{
    private readonly Mock<IDailyConsolidationRepository> _mockRepository;
    private readonly Mock<ILogger<ConsolidationService>> _mockLogger;
    private readonly ConsolidationService _service;

    public ConsolidationServiceTests()
    {
        _mockRepository = new Mock<IDailyConsolidationRepository>();
        _mockLogger = new Mock<ILogger<ConsolidationService>>();
        _service = new ConsolidationService(_mockRepository.Object, _mockLogger.Object);
    }

    [Fact]
    public async Task GetDailyConsolidationAsync_ShouldReturnConsolidationWhenExists()
    {
        // Arrange
        var merchantId = "merchant-123";
        var date = new DateOnly(2024, 1, 15);
        var expectedConsolidation = new DailyConsolidation
        {
            Id = Guid.NewGuid(),
            MerchantId = merchantId,
            Date = date,
            TotalDebits = 100m,
            TotalCredits = 200m,
            NetBalance = 100m,
            TransactionCount = 5
        };

        _mockRepository.Setup(r => r.GetByMerchantAndDateAsync(merchantId, date))
            .ReturnsAsync(expectedConsolidation);

        // Act
        var result = await _service.GetDailyConsolidationAsync(merchantId, date);

        // Assert
        Assert.NotNull(result);
        Assert.Equal(merchantId, result.MerchantId);
        Assert.Equal(date, result.Date);
        Assert.Equal(100m, result.TotalDebits);
        Assert.Equal(200m, result.TotalCredits);
        Assert.Equal(100m, result.NetBalance);
        Assert.Equal(5, result.TransactionCount);
    }

    [Fact]
    public async Task GetDailyConsolidationAsync_ShouldReturnNullWhenNotExists()
    {
        // Arrange
        var merchantId = "merchant-456";
        var date = new DateOnly(2024, 1, 16);

        _mockRepository.Setup(r => r.GetByMerchantAndDateAsync(merchantId, date))
            .ReturnsAsync((DailyConsolidation?)null);

        // Act
        var result = await _service.GetDailyConsolidationAsync(merchantId, date);

        // Assert
        Assert.Null(result);
    }

    [Fact]
    public async Task UpdateConsolidationFromTransactionAsync_ShouldCreateNewConsolidationWhenNotExists()
    {
        // Arrange
        var merchantId = "merchant-789";
        var transactionDate = new DateTime(2024, 1, 17, 10, 30, 0);
        var date = DateOnly.FromDateTime(transactionDate);

        _mockRepository.Setup(r => r.GetByMerchantAndDateAsync(merchantId, date))
            .ReturnsAsync((DailyConsolidation?)null);

        _mockRepository.Setup(r => r.CreateOrUpdateAsync(It.IsAny<DailyConsolidation>()))
            .Returns<DailyConsolidation>(consolidation => Task.FromResult(consolidation));

        // Act
        await _service.UpdateConsolidationFromTransactionAsync(merchantId, TransactionType.CREDITO, 150m, transactionDate);

        // Assert
        _mockRepository.Verify(r => r.CreateOrUpdateAsync(It.Is<DailyConsolidation>(c =>
            c.MerchantId == merchantId &&
            c.Date == date &&
            c.TotalCredits == 150m &&
            c.TotalDebits == 0m &&
            c.NetBalance == 150m &&
            c.TransactionCount == 1
        )), Times.Once);
    }

    [Fact]
    public async Task UpdateConsolidationFromTransactionAsync_ShouldUpdateExistingConsolidation()
    {
        // Arrange
        var merchantId = "merchant-101";
        var transactionDate = new DateTime(2024, 1, 18, 14, 15, 0);
        var date = DateOnly.FromDateTime(transactionDate);

        var existingConsolidation = new DailyConsolidation
        {
            Id = Guid.NewGuid(),
            MerchantId = merchantId,
            Date = date,
            TotalDebits = 50m,
            TotalCredits = 100m,
            NetBalance = 50m,
            TransactionCount = 2
        };

        _mockRepository.Setup(r => r.GetByMerchantAndDateAsync(merchantId, date))
            .ReturnsAsync(existingConsolidation);

        _mockRepository.Setup(r => r.CreateOrUpdateAsync(It.IsAny<DailyConsolidation>()))
            .Returns<DailyConsolidation>(consolidation => Task.FromResult(consolidation));

        // Act
        await _service.UpdateConsolidationFromTransactionAsync(merchantId, TransactionType.DEBITO, 75m, transactionDate);

        // Assert
        _mockRepository.Verify(r => r.CreateOrUpdateAsync(It.Is<DailyConsolidation>(c =>
            c.MerchantId == merchantId &&
            c.Date == date &&
            c.TotalDebits == 125m &&
            c.TotalCredits == 100m &&
            c.NetBalance == -25m &&
            c.TransactionCount == 3
        )), Times.Once);
    }

    [Theory]
    [InlineData(TransactionType.DEBITO, 50.0, 50.0, 0.0, -50.0)]
    [InlineData(TransactionType.CREDITO, 75.0, 0.0, 75.0, 75.0)]
    public async Task UpdateConsolidationFromTransactionAsync_ShouldHandleDifferentTransactionTypes(
        TransactionType transactionType, double amount, double expectedDebits, double expectedCredits, double expectedBalance)
    {
        // Arrange
        var merchantId = "merchant-test";
        var transactionDate = DateTime.UtcNow;
        var date = DateOnly.FromDateTime(transactionDate);

        _mockRepository.Setup(r => r.GetByMerchantAndDateAsync(merchantId, date))
            .ReturnsAsync((DailyConsolidation?)null);

        _mockRepository.Setup(r => r.CreateOrUpdateAsync(It.IsAny<DailyConsolidation>()))
            .Returns<DailyConsolidation>(consolidation => Task.FromResult(consolidation));

        // Act
        await _service.UpdateConsolidationFromTransactionAsync(merchantId, transactionType, (decimal)amount, transactionDate);

        // Assert
        _mockRepository.Verify(r => r.CreateOrUpdateAsync(It.Is<DailyConsolidation>(c =>
            c.TotalDebits == (decimal)expectedDebits &&
            c.TotalCredits == (decimal)expectedCredits &&
            c.NetBalance == (decimal)expectedBalance &&
            c.TransactionCount == 1
        )), Times.Once);
    }
}
