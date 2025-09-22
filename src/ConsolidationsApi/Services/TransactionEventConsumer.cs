using RabbitMQ.Client;
using RabbitMQ.Client.Events;
using System.Text;
using System.Text.Json;
using ConsolidationsApi.Events;
using Prometheus;

namespace ConsolidationsApi.Services;

public class TransactionEventConsumer : BackgroundService
{
    private readonly IServiceProvider _serviceProvider;
    private readonly ILogger<TransactionEventConsumer> _logger;
    private readonly IConnection _connection;
    private readonly IModel _channel;
    private readonly string _exchangeName = "cash-flow-exchange";
    private readonly string _queueName = "consolidations-queue";
    private readonly string _dlxExchangeName = "cash-flow-dlx";
    private readonly string _dlqName = "consolidations-queue-dlq";

    // Métricas básicas do consumidor
    private static readonly Counter EventsProcessedTotal = Metrics.CreateCounter(
        "cash_flow_consolidations_events_processed_total",
        "Total de eventos de transação processados com sucesso",
        new[] { "merchant_id" }
    );

    private static readonly Counter EventsFailedTotal = Metrics.CreateCounter(
        "cash_flow_consolidations_events_failed_total",
        "Total de falhas ao processar eventos de transação",
        new[] { "error_type" }
    );

    private static readonly Counter EventsDeadLetteredTotal = Metrics.CreateCounter(
        "cash_flow_consolidations_events_dead_lettered_total",
        "Total de eventos encaminhados para DLQ"
    );

    public TransactionEventConsumer(IServiceProvider serviceProvider, IConfiguration configuration, ILogger<TransactionEventConsumer> logger)
    {
        _serviceProvider = serviceProvider;
        _logger = logger;

        var conn = configuration.GetConnectionString("RabbitMQ") ?? "localhost";
        ConnectionFactory factory;
        if (Uri.TryCreate(conn, UriKind.Absolute, out var amqpUri) && (amqpUri.Scheme == "amqp" || amqpUri.Scheme == "amqps"))
        {
            factory = new ConnectionFactory { Uri = amqpUri };
        }
        else
        {
            factory = new ConnectionFactory
            {
                HostName = conn,
                Port = 5672,
                UserName = "guest",
                Password = "guest"
            };
        }

        _connection = factory.CreateConnection();
        _channel = _connection.CreateModel();

        // Exchanges principais e de DLQ
        _channel.ExchangeDeclare(_exchangeName, ExchangeType.Topic, durable: true);
        _channel.ExchangeDeclare(_dlxExchangeName, ExchangeType.Topic, durable: true);

        // Declarar DLQ
        _channel.QueueDeclare(
            queue: _dlqName,
            durable: true,
            exclusive: false,
            autoDelete: false,
            arguments: null
        );
        // Bind da DLQ ao DLX com routing key específica
        _channel.QueueBind(_dlqName, _dlxExchangeName, routingKey: "transaction.created.dlq");

        // Declarar fila principal com DLX configurado
        var queueArgs = new Dictionary<string, object>
        {
            ["x-dead-letter-exchange"] = _dlxExchangeName,
            ["x-dead-letter-routing-key"] = "transaction.created.dlq"
        };
        _channel.QueueDeclare(
            queue: _queueName,
            durable: true,
            exclusive: false,
            autoDelete: false,
            arguments: queueArgs
        );
        _channel.QueueBind(_queueName, _exchangeName, routingKey: "transaction.created");
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

                    EventsProcessedTotal.WithLabels(transactionEvent.MerchantId).Inc();

                    _logger.LogInformation("Processed transaction event for merchant {MerchantId}, transaction {TransactionId}",
                        transactionEvent.MerchantId, transactionEvent.TransactionId);
                }

                _channel.BasicAck(ea.DeliveryTag, multiple: false);
            }
            catch (Exception ex)
            {
                EventsFailedTotal.WithLabels(ex.GetType().Name).Inc();
                _logger.LogError(ex, "Error processing transaction event. Sending to DLQ.");
                // Não re-enfileirar: com DLX configurado na fila principal, vai para DLQ
                _channel.BasicNack(ea.DeliveryTag, multiple: false, requeue: false);
                EventsDeadLetteredTotal.Inc();
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
