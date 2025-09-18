using TransactionsApi.Models.Core;
using TransactionsApi.Protocols.Database;

public interface IDatabaseGateway
{
  Task<Transaction> SaveTransactionAsync(Transaction transaction);
}

public class DatabaseGateway : IDatabaseGateway
{
  private readonly IDatabaseProtocol _databaseProtocol;

  public DatabaseGateway(IDatabaseProtocol databaseProtocol)
  {
    _databaseProtocol = databaseProtocol;
  }

  public async Task<Transaction> SaveTransactionAsync(Transaction transaction)
  {
    const string sql = @"
      INSERT INTO Transactions (Id, MerchantId, Type, Amount, DateTime, Description, CreatedAt)
      VALUES (@Id, @MerchantId, @Type, @Amount, @DateTime, @Description, @CreatedAt)
      RETURNING *";

    return await _databaseProtocol.InsertAsync<Transaction>(sql, transaction);
  }
}
