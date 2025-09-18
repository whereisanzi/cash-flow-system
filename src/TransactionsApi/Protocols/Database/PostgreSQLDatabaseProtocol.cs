using System.Data;
using Npgsql;
using Dapper;

namespace TransactionsApi.Protocols.Database;

public class PostgreSQLDatabaseProtocol : IDatabaseProtocol
{
  private readonly string _connectionString;
  private IDbConnection? _connection;

  public PostgreSQLDatabaseProtocol(string connectionString)
  {
    _connectionString = connectionString;
  }

  public async Task ConnectAsync()
  {
    _connection = new NpgsqlConnection(_connectionString);
    _connection.Open();
    await Task.CompletedTask;
  }

  public async Task DisconnectAsync()
  {
    if (_connection != null)
    {
      _connection.Close();
      _connection.Dispose();
      _connection = null;
    }
    await Task.CompletedTask;
  }

  public async Task<T?> QuerySingleAsync<T>(string sql, object? parameters = null)
  {
    if (_connection == null) await ConnectAsync();
    return await _connection!.QuerySingleOrDefaultAsync<T>(sql, parameters);
  }

  public async Task<IEnumerable<T>> QueryAsync<T>(string sql, object? parameters = null)
  {
    if (_connection == null) await ConnectAsync();
    return await _connection!.QueryAsync<T>(sql, parameters);
  }

  public async Task<int> ExecuteAsync(string sql, object? parameters = null)
  {
    if (_connection == null) await ConnectAsync();
    return await _connection!.ExecuteAsync(sql, parameters);
  }

  public async Task<T> InsertAsync<T>(string sql, object parameters)
  {
    if (_connection == null) await ConnectAsync();
    return await _connection!.QuerySingleAsync<T>(sql, parameters);
  }
}