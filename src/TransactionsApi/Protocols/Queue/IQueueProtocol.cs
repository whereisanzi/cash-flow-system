namespace TransactionsApi.Protocols.Queue;

public interface IQueueProtocol
{
  Task ConnectAsync();
  Task DisconnectAsync();
  Task PublishAsync<T>(T message, string routingKey) where T : class;
  Task<T?> ConsumeAsync<T>(string queueName) where T : class;
  Task CreateQueueAsync(string queueName);
  Task DeleteQueueAsync(string queueName);
}