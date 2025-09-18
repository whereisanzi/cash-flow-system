using FluentMigrator.Runner;

namespace TransactionsApi.Protocols.Database;

public class FluentMigratorProtocol : IMigrationProtocol
{
  private readonly IMigrationRunner _runner;

  public FluentMigratorProtocol(IMigrationRunner runner)
  {
    _runner = runner;
  }

  public async Task MigrateUpAsync()
  {
    _runner.MigrateUp();
    await Task.CompletedTask;
  }

  public async Task MigrateDownAsync(long version)
  {
    _runner.MigrateDown(version);
    await Task.CompletedTask;
  }

  public async Task MigrateToVersionAsync(long version)
  {
    _runner.MigrateUp(version);
    await Task.CompletedTask;
  }

  public async Task<IEnumerable<long>> GetAppliedMigrationsAsync()
  {
    // FluentMigrator não expõe essa informação diretamente
    // Para uma implementação completa, seria necessário consultar a tabela VersionInfo
    await Task.CompletedTask;
    return new List<long>();
  }

  public async Task<IEnumerable<long>> GetPendingMigrationsAsync()
  {
    // FluentMigrator não expõe essa informação diretamente
    // Para uma implementação completa, seria necessário comparar migrations disponíveis vs aplicadas
    await Task.CompletedTask;
    return new List<long>();
  }
}