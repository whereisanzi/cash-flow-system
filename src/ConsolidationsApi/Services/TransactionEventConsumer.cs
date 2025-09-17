using RabbitMQ.Client;
using RabbitMQ.Client.Events;
using System.Text;
using System.Text.Json;
using ConsolidationsApi.Events;

namespace ConsolidationsApi.Services;

public class TransactionEventConsumer : BackgroundService
{
    private readonly IServiceProvider _serviceProvider;
    private readonly ILogger<TransactionEventConsumer> _logger;
    private readonly IConnection _connection;
    private readonly IModel _channel;
    private readonly string _exchangeName = "cash-flow-exchange";
    private readonly string _queueName = "consolidations-queue";

    public TransactionEventConsumer(IServiceProvider serviceProvider, IConfiguration configuration, ILogger<TransactionEventConsumer> logger)
    {
        _serviceProvider = serviceProvider;
        _logger = logger;

        var factory = new ConnectionFactory
        {
            HostName = configuration.GetConnectionString("RabbitMQ") ?? "localhost",
            Port = 5672,
            UserName = "guest",
            Password = "guest"
        };

        _connection = factory.CreateConnection();
        _channel = _connection.CreateModel();

        _channel.ExchangeDeclare(_exchangeName, ExchangeType.Topic, durable: true);
        _channel.QueueDeclare(_queueName, durable: true, exclusive: false, autoDelete: false);
        _channel.QueueBind(_queueName, _exchangeName, "transaction.created");
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        var consumer = new EventingBasicConsumer(_channel);

        consumer.Received += async (model, ea) =>
        {
            try
            {
                var body = ea.Body.ToArray();
                var message = Encoding.UTF8.GetString(body);

                var options = new JsonSerializerOptions
                {
                    Converters = { new System.Text.Json.Serialization.JsonStringEnumConverter() }
                };
                var transactionEvent = JsonSerializer.Deserialize<TransactionCreatedEvent>(message, options);

                if (transactionEvent != null)
                {
                    using var scope = _serviceProvider.CreateScope();
                    var consolidationService = scope.ServiceProvider.GetRequiredService<IConsolidationService>();

                    await consolidationService.UpdateConsolidationFromTransactionAsync(
                        transactionEvent.MerchantId,
                        transactionEvent.Type,
                        transactionEvent.Amount,
                        transactionEvent.DateTime);

                    _logger.LogInformation("Processed transaction event for merchant {MerchantId}, transaction {TransactionId}",
                        transactionEvent.MerchantId, transactionEvent.TransactionId);
                }

                _channel.BasicAck(ea.DeliveryTag, false);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error processing transaction event");
                _channel.BasicNack(ea.DeliveryTag, false, true);
            }
        };

        _channel.BasicConsume(_queueName, false, consumer);

        while (!stoppingToken.IsCancellationRequested)
        {
            await Task.Delay(1000, stoppingToken);
        }
    }

    public override void Dispose()
    {
        _channel?.Dispose();
        _connection?.Dispose();
        base.Dispose();
    }
}