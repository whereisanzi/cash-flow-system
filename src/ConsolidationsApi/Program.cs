using Microsoft.EntityFrameworkCore;
using ConsolidationsApi.Data;
using ConsolidationsApi.Repositories;
using ConsolidationsApi.Services;
using Prometheus;
using Serilog;


var builder = WebApplication.CreateBuilder(args);

// Configurar Serilog
Log.Logger = new LoggerConfiguration()
    .ReadFrom.Configuration(builder.Configuration)
    .Enrich.FromLogContext()
    .Enrich.WithEnvironmentName()
    .Enrich.WithMachineName()
    .Enrich.WithProperty("Service", "ConsolidationsApi")
    .WriteTo.Console(outputTemplate: "[{Timestamp:HH:mm:ss} {Level:u3}] [{Service}] {Message:lj}{NewLine}{Exception}")
    .WriteTo.File("logs/consolidations-api-.log",
        rollingInterval: RollingInterval.Day,
        outputTemplate: "{Timestamp:yyyy-MM-dd HH:mm:ss.fff zzz} [{Level:u3}] [{Service}] {Message:lj}{NewLine}{Exception}")
    .CreateLogger();

builder.Host.UseSerilog();

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

// Migrations são aplicadas por container dedicado
Log.Information("ConsolidationsApi starting - migrations handled separately");

if (app.Environment.IsDevelopment())
{
    app.UseSwagger();
    app.UseSwaggerUI();
}

// Habilitar HTTP metrics middleware
app.UseHttpMetrics();

// Endpoint para métricas do Prometheus
app.MapMetrics();

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
