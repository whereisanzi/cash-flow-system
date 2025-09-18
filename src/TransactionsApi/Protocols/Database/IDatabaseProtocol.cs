namespace TransactionsApi.Protocols.Database;

public interface IDatabaseProtocol
{
  Task ConnectAsync();
  Task DisconnectAsync();
  Task<T?> QuerySingleAsync<T>(string sql, object? parameters = null);
  Task<IEnumerable<T>> QueryAsync<T>(string sql, object? parameters = null);
  Task<int> ExecuteAsync(string sql, object? parameters = null);
  Task<T> InsertAsync<T>(string sql, object parameters);
}