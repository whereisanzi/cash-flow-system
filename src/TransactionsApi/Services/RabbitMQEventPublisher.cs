using RabbitMQ.Client;
using System.Text;
using System.Text.Json;

namespace TransactionsApi.Services;

public class RabbitMQEventPublisher : IEventPublisher, IDisposable
{
    private readonly IConnection _connection;
    private readonly IModel _channel;
    private readonly string _exchangeName;

    public RabbitMQEventPublisher(IConfiguration configuration)
    {
        var factory = new ConnectionFactory
        {
            HostName = configuration.GetConnectionString("RabbitMQ") ?? "localhost",
            Port = 5672,
            UserName = "guest",
            Password = "guest"
        };

        _connection = factory.CreateConnection();
        _channel = _connection.CreateModel();
        _exchangeName = "cash-flow-exchange";

        _channel.ExchangeDeclare(_exchangeName, ExchangeType.Topic, durable: true);
    }

    public async Task PublishAsync<T>(T eventMessage, string routingKey) where T : class
    {
        var message = JsonSerializer.Serialize(eventMessage);
        var body = Encoding.UTF8.GetBytes(message);

        var properties = _channel.CreateBasicProperties();
        properties.Persistent = true;
        properties.Timestamp = new AmqpTimestamp(DateTimeOffset.UtcNow.ToUnixTimeSeconds());

        _channel.BasicPublish(
            exchange: _exchangeName,
            routingKey: routingKey,
            mandatory: false,
            basicProperties: properties,
            body: body
        );

        await Task.CompletedTask;
    }

    public void Dispose()
    {
        _channel?.Dispose();
        _connection?.Dispose();
    }
}