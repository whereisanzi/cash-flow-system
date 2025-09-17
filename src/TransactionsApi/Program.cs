using Microsoft.EntityFrameworkCore;
using System.ComponentModel.DataAnnotations;
using TransactionsApi.Data;
using TransactionsApi.DTOs;
using TransactionsApi.Repositories;
using TransactionsApi.Services;

var builder = WebApplication.CreateBuilder(args);

builder.Services.AddDbContext<TransactionsDbContext>(options =>
    options.UseNpgsql(builder.Configuration.GetConnectionString("DefaultConnection")));

builder.Services.AddScoped<ITransactionRepository, TransactionRepository>();
builder.Services.AddScoped<ITransactionService, TransactionService>();
builder.Services.AddSingleton<IEventPublisher, RabbitMQEventPublisher>();

builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen();

// Configurar JSON para aceitar strings em enums
builder.Services.ConfigureHttpJsonOptions(options =>
{
    options.SerializerOptions.Converters.Add(new System.Text.Json.Serialization.JsonStringEnumConverter());
});

var app = builder.Build();

// Aplicar migrations automaticamente
using (var scope = app.Services.CreateScope())
{
    var context = scope.ServiceProvider.GetRequiredService<TransactionsDbContext>();
    try
    {
        context.Database.Migrate();
    }
    catch (InvalidOperationException ex) when (ex.Message.Contains("PendingModelChangesWarning"))
    {
        // Se há mudanças pendentes no modelo, criar/recriar o banco
        context.Database.EnsureDeleted();
        context.Database.EnsureCreated();
    }
}

if (app.Environment.IsDevelopment())
{
    app.UseSwagger();
    app.UseSwaggerUI();
}

app.MapPost("/api/v1/merchants/{merchantId}/transactions", async (
    string merchantId,
    CreateTransactionRequest request,
    ITransactionService transactionService) =>
{
    if (string.IsNullOrWhiteSpace(merchantId))
        return Results.BadRequest("MerchantId is required");

    var validationResults = new List<ValidationResult>();
    var validationContext = new ValidationContext(request);

    if (!Validator.TryValidateObject(request, validationContext, validationResults, true))
    {
        var errors = validationResults.Select(vr => vr.ErrorMessage).ToList();
        return Results.BadRequest(new { Errors = errors });
    }

    try
    {
        var result = await transactionService.CreateTransactionAsync(merchantId, request);
        return Results.Created($"/api/v1/transactions/{result.Id}", result);
    }
    catch (Exception ex)
    {
        return Results.Problem($"An error occurred: {ex.Message}");
    }
})
.WithName("CreateTransaction")
.WithOpenApi();

app.MapGet("/health", () => Results.Ok(new { Status = "Healthy", Service = "TransactionsApi" }))
.WithName("HealthCheck")
.WithOpenApi();

app.Run();
