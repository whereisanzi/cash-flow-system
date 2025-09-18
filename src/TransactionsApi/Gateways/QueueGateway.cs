using TransactionsApi.Models.Core;
using TransactionsApi.Protocols.Queue;

public interface IQueueGateway
{
  Task PublishTransactionCreatedAsync(Transaction transaction);
}

public class QueueGateway : IQueueGateway
{
  private readonly IQueueProtocol _queueProtocol;
  private readonly ITransactionAdapter _transactionAdapter;

  public QueueGateway(IQueueProtocol queueProtocol, ITransactionAdapter transactionAdapter)
  {
    _queueProtocol = queueProtocol;
    _transactionAdapter = transactionAdapter;
  }

  public async Task PublishTransactionCreatedAsync(Transaction transaction)
  {
    var eventData = _transactionAdapter.ToEvent(transaction);
    await _queueProtocol.PublishAsync(eventData, "transaction.created");
  }
}
