using System.Data;
using Npgsql;
using Dapper;

namespace TransactionsApi.Protocols.Database;

public class PostgreSQLDatabaseProtocol : IDatabaseProtocol, IDisposable
{
  private readonly string _connectionString;

  public PostgreSQLDatabaseProtocol(string connectionString)
  {
    _connectionString = connectionString;
  }

  private async Task<IDbConnection> CreateConnectionAsync()
  {
    var connection = new NpgsqlConnection(_connectionString);
    await connection.OpenAsync();
    return connection;
  }

  public async Task ConnectAsync()
  {
    // No longer needed with new pattern, kept for interface compatibility
    await Task.CompletedTask;
  }

  public async Task DisconnectAsync()
  {
    // No longer needed with new pattern, kept for interface compatibility
    await Task.CompletedTask;
  }

  public async Task<T?> QuerySingleAsync<T>(string sql, object? parameters = null)
  {
    using var connection = await CreateConnectionAsync();
    return await connection.QuerySingleOrDefaultAsync<T>(sql, parameters);
  }

  public async Task<IEnumerable<T>> QueryAsync<T>(string sql, object? parameters = null)
  {
    using var connection = await CreateConnectionAsync();
    return await connection.QueryAsync<T>(sql, parameters);
  }

  public async Task<int> ExecuteAsync(string sql, object? parameters = null)
  {
    using var connection = await CreateConnectionAsync();
    return await connection.ExecuteAsync(sql, parameters);
  }

  public async Task<T> InsertAsync<T>(string sql, object parameters)
  {
    using var connection = await CreateConnectionAsync();
    return await connection.QuerySingleAsync<T>(sql, parameters);
  }

  public void Dispose()
  {
    // Nothing to dispose in the new pattern
  }
}