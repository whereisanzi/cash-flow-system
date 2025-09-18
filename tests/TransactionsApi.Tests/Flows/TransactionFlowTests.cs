using TransactionsApi.Models.Core;
using TransactionsApi.Models.Data;
using Moq;
using Xunit;

namespace TransactionsApi.Tests.Flows;

public class TransactionFlowTests
{
    private readonly Mock<IDatabaseGateway> _mockDatabaseGateway;
    private readonly Mock<IQueueGateway> _mockQueueGateway;
    private readonly Mock<ITransactionAdapter> _mockTransactionAdapter;
    private readonly Mock<ITransactionLogic> _mockTransactionLogic;

    public TransactionFlowTests()
    {
        _mockDatabaseGateway = new Mock<IDatabaseGateway>();
        _mockQueueGateway = new Mock<IQueueGateway>();
        _mockTransactionAdapter = new Mock<ITransactionAdapter>();
        _mockTransactionLogic = new Mock<ITransactionLogic>();
    }

    [Fact]
    public async Task CreateTransaction_ShouldExecuteCompleteFlow()
    {
        // Arrange
        var merchantId = "merchant-123";
        var request = new CreateTransactionRequest
        {
            Type = TransactionType.CREDITO,
            Amount = 100.50m,
            Description = "Test transaction"
        };

        var coreTransaction = new Transaction
        {
            MerchantId = merchantId,
            Type = request.Type,
            Amount = request.Amount,
            Description = request.Description
        };

        var enrichedTransaction = new Transaction
        {
            Id = Guid.NewGuid(),
            MerchantId = merchantId,
            Type = request.Type,
            Amount = request.Amount,
            Description = request.Description,
            DateTime = DateTime.UtcNow,
            CreatedAt = DateTime.UtcNow
        };

        var savedTransaction = new Transaction
        {
            Id = enrichedTransaction.Id,
            MerchantId = merchantId,
            Type = request.Type,
            Amount = request.Amount,
            Description = request.Description,
            DateTime = enrichedTransaction.DateTime,
            CreatedAt = enrichedTransaction.CreatedAt
        };

        var expectedResponse = new TransactionResponse
        {
            Id = savedTransaction.Id,
            MerchantId = merchantId,
            Type = request.Type,
            Amount = request.Amount,
            DateTime = savedTransaction.DateTime,
            Description = request.Description,
            CreatedAt = savedTransaction.CreatedAt
        };

        _mockTransactionAdapter.Setup(x => x.ToCore(merchantId, request))
            .Returns(coreTransaction);

        _mockTransactionLogic.Setup(x => x.ValidateAndEnrich(coreTransaction))
            .Returns(enrichedTransaction);

        _mockDatabaseGateway.Setup(x => x.SaveTransactionAsync(enrichedTransaction))
            .ReturnsAsync(savedTransaction);

        _mockTransactionAdapter.Setup(x => x.ToResponse(savedTransaction))
            .Returns(expectedResponse);

        // Act
        var result = await TransactionFlow.CreateTransaction(
            merchantId,
            request,
            _mockDatabaseGateway.Object,
            _mockQueueGateway.Object,
            _mockTransactionAdapter.Object,
            _mockTransactionLogic.Object);

        // Assert
        Assert.Equal(expectedResponse.Id, result.Id);
        Assert.Equal(expectedResponse.MerchantId, result.MerchantId);
        Assert.Equal(expectedResponse.Type, result.Type);
        Assert.Equal(expectedResponse.Amount, result.Amount);
        Assert.Equal(expectedResponse.DateTime, result.DateTime);
        Assert.Equal(expectedResponse.Description, result.Description);
        Assert.Equal(expectedResponse.CreatedAt, result.CreatedAt);

        // Verify all dependencies were called in correct order
        _mockTransactionLogic.Verify(x => x.ValidateRequest(merchantId, request), Times.Once);
        _mockTransactionAdapter.Verify(x => x.ToCore(merchantId, request), Times.Once);
        _mockTransactionLogic.Verify(x => x.ValidateAndEnrich(coreTransaction), Times.Once);
        _mockDatabaseGateway.Verify(x => x.SaveTransactionAsync(enrichedTransaction), Times.Once);
        _mockQueueGateway.Verify(x => x.PublishTransactionCreatedAsync(savedTransaction), Times.Once);
        _mockTransactionAdapter.Verify(x => x.ToResponse(savedTransaction), Times.Once);
    }

    [Fact]
    public async Task CreateTransaction_ShouldThrowWhenValidationFails()
    {
        // Arrange
        var merchantId = "merchant-123";
        var request = new CreateTransactionRequest
        {
            Type = TransactionType.CREDITO,
            Amount = -100m,
            Description = "Invalid transaction"
        };

        _mockTransactionLogic.Setup(x => x.ValidateRequest(merchantId, request))
            .Throws(new ArgumentException("Amount must be greater than zero"));

        // Act & Assert
        var exception = await Assert.ThrowsAsync<ArgumentException>(() =>
            TransactionFlow.CreateTransaction(
                merchantId,
                request,
                _mockDatabaseGateway.Object,
                _mockQueueGateway.Object,
                _mockTransactionAdapter.Object,
                _mockTransactionLogic.Object));

        Assert.Equal("Amount must be greater than zero", exception.Message);

        // Verify only validation was called
        _mockTransactionLogic.Verify(x => x.ValidateRequest(merchantId, request), Times.Once);
        _mockTransactionAdapter.Verify(x => x.ToCore(It.IsAny<string>(), It.IsAny<CreateTransactionRequest>()), Times.Never);
        _mockTransactionLogic.Verify(x => x.ValidateAndEnrich(It.IsAny<Transaction>()), Times.Never);
        _mockDatabaseGateway.Verify(x => x.SaveTransactionAsync(It.IsAny<Transaction>()), Times.Never);
        _mockQueueGateway.Verify(x => x.PublishTransactionCreatedAsync(It.IsAny<Transaction>()), Times.Never);
        _mockTransactionAdapter.Verify(x => x.ToResponse(It.IsAny<Transaction>()), Times.Never);
    }

    [Fact]
    public async Task CreateTransaction_ShouldThrowWhenEnrichmentFails()
    {
        // Arrange
        var merchantId = "merchant-123";
        var request = new CreateTransactionRequest
        {
            Type = TransactionType.CREDITO,
            Amount = 100m,
            Description = "Test transaction"
        };

        var coreTransaction = new Transaction
        {
            MerchantId = merchantId,
            Type = request.Type,
            Amount = request.Amount,
            Description = request.Description
        };

        _mockTransactionAdapter.Setup(x => x.ToCore(merchantId, request))
            .Returns(coreTransaction);

        _mockTransactionLogic.Setup(x => x.ValidateAndEnrich(coreTransaction))
            .Throws(new ArgumentException("Enrichment failed"));

        // Act & Assert
        var exception = await Assert.ThrowsAsync<ArgumentException>(() =>
            TransactionFlow.CreateTransaction(
                merchantId,
                request,
                _mockDatabaseGateway.Object,
                _mockQueueGateway.Object,
                _mockTransactionAdapter.Object,
                _mockTransactionLogic.Object));

        Assert.Equal("Enrichment failed", exception.Message);

        // Verify flow stopped at enrichment
        _mockTransactionLogic.Verify(x => x.ValidateRequest(merchantId, request), Times.Once);
        _mockTransactionAdapter.Verify(x => x.ToCore(merchantId, request), Times.Once);
        _mockTransactionLogic.Verify(x => x.ValidateAndEnrich(coreTransaction), Times.Once);
        _mockDatabaseGateway.Verify(x => x.SaveTransactionAsync(It.IsAny<Transaction>()), Times.Never);
        _mockQueueGateway.Verify(x => x.PublishTransactionCreatedAsync(It.IsAny<Transaction>()), Times.Never);
        _mockTransactionAdapter.Verify(x => x.ToResponse(It.IsAny<Transaction>()), Times.Never);
    }

    [Fact]
    public async Task CreateTransaction_ShouldThrowWhenDatabaseSaveFails()
    {
        // Arrange
        var merchantId = "merchant-123";
        var request = new CreateTransactionRequest
        {
            Type = TransactionType.CREDITO,
            Amount = 100m,
            Description = "Test transaction"
        };

        var coreTransaction = new Transaction
        {
            MerchantId = merchantId,
            Type = request.Type,
            Amount = request.Amount,
            Description = request.Description
        };

        var enrichedTransaction = new Transaction
        {
            Id = Guid.NewGuid(),
            MerchantId = merchantId,
            Type = request.Type,
            Amount = request.Amount,
            Description = request.Description,
            DateTime = DateTime.UtcNow,
            CreatedAt = DateTime.UtcNow
        };

        _mockTransactionAdapter.Setup(x => x.ToCore(merchantId, request))
            .Returns(coreTransaction);

        _mockTransactionLogic.Setup(x => x.ValidateAndEnrich(coreTransaction))
            .Returns(enrichedTransaction);

        _mockDatabaseGateway.Setup(x => x.SaveTransactionAsync(enrichedTransaction))
            .ThrowsAsync(new InvalidOperationException("Database save failed"));

        // Act & Assert
        var exception = await Assert.ThrowsAsync<InvalidOperationException>(() =>
            TransactionFlow.CreateTransaction(
                merchantId,
                request,
                _mockDatabaseGateway.Object,
                _mockQueueGateway.Object,
                _mockTransactionAdapter.Object,
                _mockTransactionLogic.Object));

        Assert.Equal("Database save failed", exception.Message);

        // Verify flow stopped at database save
        _mockTransactionLogic.Verify(x => x.ValidateRequest(merchantId, request), Times.Once);
        _mockTransactionAdapter.Verify(x => x.ToCore(merchantId, request), Times.Once);
        _mockTransactionLogic.Verify(x => x.ValidateAndEnrich(coreTransaction), Times.Once);
        _mockDatabaseGateway.Verify(x => x.SaveTransactionAsync(enrichedTransaction), Times.Once);
        _mockQueueGateway.Verify(x => x.PublishTransactionCreatedAsync(It.IsAny<Transaction>()), Times.Never);
        _mockTransactionAdapter.Verify(x => x.ToResponse(It.IsAny<Transaction>()), Times.Never);
    }

    [Fact]
    public async Task CreateTransaction_ShouldContinueWhenQueuePublishFails()
    {
        // Arrange
        var merchantId = "merchant-123";
        var request = new CreateTransactionRequest
        {
            Type = TransactionType.CREDITO,
            Amount = 100m,
            Description = "Test transaction"
        };

        var coreTransaction = new Transaction
        {
            MerchantId = merchantId,
            Type = request.Type,
            Amount = request.Amount,
            Description = request.Description
        };

        var enrichedTransaction = new Transaction
        {
            Id = Guid.NewGuid(),
            MerchantId = merchantId,
            Type = request.Type,
            Amount = request.Amount,
            Description = request.Description,
            DateTime = DateTime.UtcNow,
            CreatedAt = DateTime.UtcNow
        };

        var savedTransaction = new Transaction
        {
            Id = enrichedTransaction.Id,
            MerchantId = merchantId,
            Type = request.Type,
            Amount = request.Amount,
            Description = request.Description,
            DateTime = enrichedTransaction.DateTime,
            CreatedAt = enrichedTransaction.CreatedAt
        };

        var expectedResponse = new TransactionResponse
        {
            Id = savedTransaction.Id,
            MerchantId = merchantId,
            Type = request.Type,
            Amount = request.Amount,
            DateTime = savedTransaction.DateTime,
            Description = request.Description,
            CreatedAt = savedTransaction.CreatedAt
        };

        _mockTransactionAdapter.Setup(x => x.ToCore(merchantId, request))
            .Returns(coreTransaction);

        _mockTransactionLogic.Setup(x => x.ValidateAndEnrich(coreTransaction))
            .Returns(enrichedTransaction);

        _mockDatabaseGateway.Setup(x => x.SaveTransactionAsync(enrichedTransaction))
            .ReturnsAsync(savedTransaction);

        _mockQueueGateway.Setup(x => x.PublishTransactionCreatedAsync(savedTransaction))
            .ThrowsAsync(new InvalidOperationException("Queue publish failed"));

        _mockTransactionAdapter.Setup(x => x.ToResponse(savedTransaction))
            .Returns(expectedResponse);

        // Act & Assert - Queue failure should propagate
        var exception = await Assert.ThrowsAsync<InvalidOperationException>(() =>
            TransactionFlow.CreateTransaction(
                merchantId,
                request,
                _mockDatabaseGateway.Object,
                _mockQueueGateway.Object,
                _mockTransactionAdapter.Object,
                _mockTransactionLogic.Object));

        Assert.Equal("Queue publish failed", exception.Message);

        // Verify all steps up to queue were executed
        _mockTransactionLogic.Verify(x => x.ValidateRequest(merchantId, request), Times.Once);
        _mockTransactionAdapter.Verify(x => x.ToCore(merchantId, request), Times.Once);
        _mockTransactionLogic.Verify(x => x.ValidateAndEnrich(coreTransaction), Times.Once);
        _mockDatabaseGateway.Verify(x => x.SaveTransactionAsync(enrichedTransaction), Times.Once);
        _mockQueueGateway.Verify(x => x.PublishTransactionCreatedAsync(savedTransaction), Times.Once);
        _mockTransactionAdapter.Verify(x => x.ToResponse(It.IsAny<Transaction>()), Times.Never);
    }

    [Theory]
    [InlineData(TransactionType.DEBITO, 50.25)]
    [InlineData(TransactionType.CREDITO, 150.75)]
    public async Task CreateTransaction_ShouldHandleDifferentTransactionTypes(TransactionType type, decimal amount)
    {
        // Arrange
        var merchantId = "merchant-test";
        var request = new CreateTransactionRequest
        {
            Type = type,
            Amount = amount,
            Description = "Type test"
        };

        var coreTransaction = new Transaction
        {
            MerchantId = merchantId,
            Type = type,
            Amount = amount,
            Description = "Type test"
        };

        var enrichedTransaction = new Transaction
        {
            Id = Guid.NewGuid(),
            MerchantId = merchantId,
            Type = type,
            Amount = amount,
            Description = "Type test",
            DateTime = DateTime.UtcNow,
            CreatedAt = DateTime.UtcNow
        };

        var savedTransaction = new Transaction
        {
            Id = enrichedTransaction.Id,
            MerchantId = merchantId,
            Type = type,
            Amount = amount,
            Description = "Type test",
            DateTime = enrichedTransaction.DateTime,
            CreatedAt = enrichedTransaction.CreatedAt
        };

        var expectedResponse = new TransactionResponse
        {
            Id = savedTransaction.Id,
            MerchantId = merchantId,
            Type = type,
            Amount = amount,
            DateTime = savedTransaction.DateTime,
            Description = "Type test",
            CreatedAt = savedTransaction.CreatedAt
        };

        _mockTransactionAdapter.Setup(x => x.ToCore(merchantId, request))
            .Returns(coreTransaction);

        _mockTransactionLogic.Setup(x => x.ValidateAndEnrich(coreTransaction))
            .Returns(enrichedTransaction);

        _mockDatabaseGateway.Setup(x => x.SaveTransactionAsync(enrichedTransaction))
            .ReturnsAsync(savedTransaction);

        _mockTransactionAdapter.Setup(x => x.ToResponse(savedTransaction))
            .Returns(expectedResponse);

        // Act
        var result = await TransactionFlow.CreateTransaction(
            merchantId,
            request,
            _mockDatabaseGateway.Object,
            _mockQueueGateway.Object,
            _mockTransactionAdapter.Object,
            _mockTransactionLogic.Object);

        // Assert
        Assert.Equal(type, result.Type);
        Assert.Equal(amount, result.Amount);
        Assert.Equal("Type test", result.Description);
    }
}