using TransactionsApi.Models.Data;
using TransactionsApi.Protocols.Database;
using TransactionsApi.Protocols.Queue;
using FluentMigrator.Runner;

var builder = WebApplication.CreateBuilder(args);



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

// Aplicar FluentMigrator migrations automaticamente
using (var scope = app.Services.CreateScope())
{
    var migrationProtocol = scope.ServiceProvider.GetRequiredService<IMigrationProtocol>();
    await migrationProtocol.MigrateUpAsync();
}

if (app.Environment.IsDevelopment())
{
    app.UseSwagger();
    app.UseSwaggerUI();
}



app
.MapPost("/api/v1/merchants/{merchantId}/transactions", TransactionHandler.CreateTransaction)
.WithName("CreateTransaction")
.WithOpenApi();

app
.MapGet("/health", TransactionHandler.HealthCheck)
.WithName("HealthCheck")
.WithOpenApi();

app.Run();
