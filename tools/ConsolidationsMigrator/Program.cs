using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Serilog;
using ConsolidationsApi.Data;

Log.Logger = new LoggerConfiguration()
    .WriteTo.Console()
    .CreateLogger();

try
{
    Log.Information("🔄 ConsolidationsMigrator starting...");

    var connectionString = Environment.GetEnvironmentVariable("ConnectionStrings__DefaultConnection");

    if (string.IsNullOrEmpty(connectionString))
    {
        Log.Error("❌ ConnectionStrings__DefaultConnection environment variable not found");
        return 1;
    }

    Log.Information("📊 Using connection: {ConnectionString}", connectionString.Replace("Password=postgres123", "Password=***"));

    // Configurar serviços para Entity Framework
    var services = new ServiceCollection()
        .AddDbContext<ConsolidationsDbContext>(options =>
            options.UseNpgsql(connectionString))
        .AddLogging(lb => lb.AddSerilog());

    using var serviceProvider = services.BuildServiceProvider(false);
    using var scope = serviceProvider.CreateScope();

    var context = scope.ServiceProvider.GetRequiredService<ConsolidationsDbContext>();

    Log.Information("⚡ Applying Entity Framework migrations...");
    await context.Database.MigrateAsync();

    Log.Information("✅ All migrations applied successfully!");
    return 0;
}
catch (Exception ex)
{
    Log.Error(ex, "❌ Migration failed: {Message}", ex.Message);
    return 1;
}
finally
{
    Log.CloseAndFlush();
}