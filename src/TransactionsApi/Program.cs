using TransactionsApi.Models.Data;
using TransactionsApi.Protocols.Database;
using TransactionsApi.Protocols.Queue;
using FluentMigrator.Runner;
using Prometheus;
using Serilog;

var builder = WebApplication.CreateBuilder(args);

// Configurar Serilog
Log.Logger = new LoggerConfiguration()
    .ReadFrom.Configuration(builder.Configuration)
    .Enrich.FromLogContext()
    .Enrich.WithEnvironmentName()
    .Enrich.WithMachineName()
    .Enrich.WithProperty("Service", "TransactionsApi")
    .WriteTo.Console(outputTemplate: "[{Timestamp:HH:mm:ss} {Level:u3}] [{Service}] {Message:lj}{NewLine}{Exception}")
    .WriteTo.File("logs/transactions-api-.log",
        rollingInterval: RollingInterval.Day,
        outputTemplate: "{Timestamp:yyyy-MM-dd HH:mm:ss.fff zzz} [{Level:u3}] [{Service}] {Message:lj}{NewLine}{Exception}")
    .CreateLogger();

builder.Host.UseSerilog();



builder.Services.AddScoped<IDatabaseProtocol>(provider =>
  new PostgreSQLDatabaseProtocol(builder.Configuration.GetConnectionString("DefaultConnection")!));
builder.Services.AddScoped<IQueueProtocol>(provider =>
  new RabbitMQQueueProtocol(builder.Configuration.GetConnectionString("RabbitMQ")!));
builder.Services.AddScoped<IMigrationProtocol, FluentMigratorProtocol>();

builder.Services.AddScoped<IDatabaseGateway, DatabaseGateway>();
builder.Services.AddScoped<IQueueGateway, QueueGateway>();
builder.Services.AddScoped<ITransactionAdapter, TransactionAdapter>();
builder.Services.AddScoped<ITransactionLogic, TransactionLogic>();

builder.Services.AddFluentMigratorCore()
    .ConfigureRunner(rb => rb
        .AddPostgres()
        .WithGlobalConnectionString(builder.Configuration.GetConnectionString("DefaultConnection"))
        .ScanIn(typeof(Program).Assembly).For.Migrations())
    .AddLogging(lb => lb.AddFluentMigratorConsole());

builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen();


// Configurar JSON para aceitar strings em enums
builder.Services.ConfigureHttpJsonOptions(options =>
{
    options.SerializerOptions.Converters.Add(new System.Text.Json.Serialization.JsonStringEnumConverter());
});

var app = builder.Build();

// Migrations são aplicadas por container dedicado
Log.Information("TransactionsApi starting - migrations handled separately");

if (app.Environment.IsDevelopment())
{
    app.UseSwagger();
    app.UseSwaggerUI();
}

// Habilitar HTTP metrics middleware
app.UseHttpMetrics();

// Endpoint para métricas do Prometheus
app.MapMetrics();



app
.MapPost("/api/v1/merchants/{merchantId}/transactions", TransactionHandler.CreateTransaction)
.WithName("CreateTransaction")
.WithOpenApi();

app
.MapGet("/health", TransactionHandler.HealthCheck)
.WithName("HealthCheck")
.WithOpenApi();

app.Run();
