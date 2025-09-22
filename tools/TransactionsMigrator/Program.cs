using FluentMigrator.Runner;
using Microsoft.Extensions.DependencyInjection;
using Serilog;

Log.Logger = new LoggerConfiguration()
    .WriteTo.Console()
    .CreateLogger();

try
{
    Log.Information("🔄 TransactionsMigrator starting...");

    var connectionString = Environment.GetEnvironmentVariable("ConnectionStrings__DefaultConnection");

    if (string.IsNullOrEmpty(connectionString))
    {
        Log.Error("❌ ConnectionStrings__DefaultConnection environment variable not found");
        return 1;
    }

    Log.Information("📊 Using connection: {ConnectionString}", connectionString.Replace("Password=postgres123", "Password=***"));

    // Configurar serviços para FluentMigrator
    var services = new ServiceCollection()
        .AddFluentMigratorCore()
        .ConfigureRunner(rb => rb
            .AddPostgres()
            .WithGlobalConnectionString(connectionString)
            .ScanIn(typeof(Program).Assembly).For.Migrations())
        .AddLogging(lb => lb.AddSerilog());

    using var serviceProvider = services.BuildServiceProvider(false);
    using var scope = serviceProvider.CreateScope();

    var runner = scope.ServiceProvider.GetRequiredService<IMigrationRunner>();

    Log.Information("⚡ Applying FluentMigrator migrations...");
    runner.MigrateUp();

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