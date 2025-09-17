using Microsoft.EntityFrameworkCore;
using ConsolidationsApi.Data;
using ConsolidationsApi.Repositories;
using ConsolidationsApi.Services;

var builder = WebApplication.CreateBuilder(args);

builder.Services.AddDbContext<ConsolidationsDbContext>(options =>
    options.UseNpgsql(builder.Configuration.GetConnectionString("DefaultConnection")));

builder.Services.AddScoped<IDailyConsolidationRepository, DailyConsolidationRepository>();
builder.Services.AddScoped<IConsolidationService, ConsolidationService>();
builder.Services.AddHostedService<TransactionEventConsumer>();

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
    var context = scope.ServiceProvider.GetRequiredService<ConsolidationsDbContext>();
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

app.MapGet("/api/v1/merchants/{merchantId}/consolidations/daily", async (
    string merchantId,
    DateOnly date,
    IConsolidationService consolidationService) =>
{
    if (string.IsNullOrWhiteSpace(merchantId))
        return Results.BadRequest("MerchantId is required");

    try
    {
        var result = await consolidationService.GetDailyConsolidationAsync(merchantId, date);
        if (result == null)
            return Results.NotFound("Consolidation not found for the specified date");

        return Results.Ok(result);
    }
    catch (Exception ex)
    {
        return Results.Problem($"An error occurred: {ex.Message}");
    }
})
.WithName("GetDailyConsolidation")
.WithOpenApi();

app.MapGet("/health", () => Results.Ok(new { Status = "Healthy", Service = "ConsolidationsApi" }))
.WithName("HealthCheck")
.WithOpenApi();

app.Run();
