using System.Data;
using Microsoft.Data.Sqlite;
using PromarketPagamentoApi.Contracts;

namespace PromarketPagamentoApi.Data;

public sealed class SqliteConnectionFactory(string connectionString) : IDbConnectionFactory
{
    public IDbConnection CreateConnection() => new SqliteConnection(connectionString);
}