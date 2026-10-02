using System.Data;
using Dapper;
using PromarketPagamentoApi.Contracts;

namespace PromarketPagamentoApi.Data;

public static class DatabaseSetup
{
    public static async Task InicializarBancoAsync(
        IDbConnectionFactory factory,
        CancellationToken ct = default)
    {
        using var connection = factory.CreateConnection();
        if (connection.State != ConnectionState.Open)
        {
            connection.Open();
        }

        using var transaction = connection.BeginTransaction(IsolationLevel.Serializable);

        try
        {
            var eventColumns = await GetColumnsAsync(connection, "eventos_processados", transaction, ct);
            var migrateEvents = eventColumns.Contains("EventoId") && !eventColumns.Contains("evento_id");
            if (migrateEvents)
            {
                await connection.ExecuteAsync(new CommandDefinition(
                    "ALTER TABLE eventos_processados RENAME TO eventos_processados_legacy;",
                    transaction: transaction,
                    cancellationToken: ct));
            }

            var orderColumns = await GetColumnsAsync(connection, "pedidos", transaction, ct);
            var migrateOrders = orderColumns.Contains("PedidoId") && !orderColumns.Contains("id");
            if (migrateOrders)
            {
                await connection.ExecuteAsync(new CommandDefinition(
                    "ALTER TABLE pedidos RENAME TO pedidos_legacy;",
                    transaction: transaction,
                    cancellationToken: ct));
            }

            const string createTablesSql = @"
                CREATE TABLE IF NOT EXISTS eventos_processados (
                    evento_id TEXT PRIMARY KEY,
                    pedido_id TEXT NOT NULL,
                    valor NUMERIC NOT NULL,
                    processado_em TEXT NOT NULL
                );

                CREATE TABLE IF NOT EXISTS pedidos (
                    id TEXT PRIMARY KEY,
                    saldo_creditado NUMERIC NOT NULL DEFAULT 0,
                    atualizado_em TEXT NOT NULL
                );";

            await connection.ExecuteAsync(new CommandDefinition(
                createTablesSql,
                transaction: transaction,
                cancellationToken: ct));

            if (migrateEvents)
            {
                await connection.ExecuteAsync(new CommandDefinition(
                    @"INSERT INTO eventos_processados (evento_id, pedido_id, valor, processado_em)
                      SELECT EventoId, PedidoId, 0, CAST(DataProcessamento AS TEXT)
                      FROM eventos_processados_legacy;
                      DROP TABLE eventos_processados_legacy;",
                    transaction: transaction,
                    cancellationToken: ct));
            }

            if (migrateOrders)
            {
                await connection.ExecuteAsync(new CommandDefinition(
                    @"INSERT INTO pedidos (id, saldo_creditado, atualizado_em)
                      SELECT PedidoId, SaldoCreditado, @MigratedAt
                      FROM pedidos_legacy;
                      DROP TABLE pedidos_legacy;",
                    new { MigratedAt = DateTime.UtcNow.ToString("O") },
                    transaction,
                    cancellationToken: ct));
            }

            transaction.Commit();
        }
        catch
        {
            transaction.Rollback();
            throw;
        }
    }

    private static async Task<HashSet<string>> GetColumnsAsync(
        IDbConnection connection,
        string tableName,
        IDbTransaction transaction,
        CancellationToken ct)
    {
        var columns = await connection.QueryAsync<string>(new CommandDefinition(
            "SELECT name FROM pragma_table_info(@TableName);",
            new { TableName = tableName },
            transaction: transaction,
            cancellationToken: ct));

        return columns.ToHashSet(StringComparer.OrdinalIgnoreCase);
    }
}