using System.Data;

namespace PromarketPagamentoApi.Contracts;

public interface IDbConnectionFactory
{
    IDbConnection CreateConnection();
}