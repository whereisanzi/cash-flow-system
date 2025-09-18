using System.Text;
using System.Text.Json;
using RabbitMQ.Client;

namespace TransactionsApi.Protocols.Queue;

public class RabbitMQQueueProtocol : IQueueProtocol
{
  private readonly string _connectionString;
  private IConnection? _connection;
  private IModel? _channel;

  public RabbitMQQueueProtocol(string connectionString)
  {
    _connectionString = connectionString;
  }

  public async Task ConnectAsync()
  {
    var factory = new ConnectionFactory() { Uri = new Uri(_connectionString) };
    _connection = factory.CreateConnection();
    _channel = _connection.CreateModel();
    await Task.CompletedTask;
  }

  public async Task DisconnectAsync()
  {
    _channel?.Close();
    _channel?.Dispose();
    _connection?.Close();
    _connection?.Dispose();
    _channel = null;
    _connection = null;
    await Task.CompletedTask;
  }

  public async Task PublishAsync<T>(T message, string routingKey) where T : class
  {
    if (_channel == null) await ConnectAsync();

    var json = JsonSerializer.Serialize(message);
    var body = Encoding.UTF8.GetBytes(json);

    _channel!.BasicPublish(exchange: "", routingKey: routingKey, basicProperties: null, body: body);
    await Task.CompletedTask;
  }

  public async Task<T?> ConsumeAsync<T>(string queueName) where T : class
  {
    if (_channel == null) await ConnectAsync();

    var result = _channel!.BasicGet(queueName, autoAck: true);
    if (result == null) return null;

    var json = Encoding.UTF8.GetString(result.Body.ToArray());
    return JsonSerializer.Deserialize<T>(json);
  }

  public async Task CreateQueueAsync(string queueName)
  {
    if (_channel == null) await ConnectAsync();
    _channel!.QueueDeclare(queue: queueName, durable: true, exclusive: false, autoDelete: false, arguments: null);
    await Task.CompletedTask;
  }

  public async Task DeleteQueueAsync(string queueName)
  {
    if (_channel == null) await ConnectAsync();
    _channel!.QueueDelete(queueName);
    await Task.CompletedTask;
  }
}