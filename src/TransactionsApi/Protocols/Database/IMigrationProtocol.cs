namespace TransactionsApi.Protocols.Database;

public interface IMigrationProtocol
{
  Task MigrateUpAsync();
  Task MigrateDownAsync(long version);
  Task MigrateToVersionAsync(long version);
  Task<IEnumerable<long>> GetAppliedMigrationsAsync();
  Task<IEnumerable<long>> GetPendingMigrationsAsync();
}