using System.Data;
using Dapper;
using Microsoft.Data.Sqlite;
using PromarketPagamentoApi.Contracts;
using PromarketPagamentoApi.DTOs;

namespace PromarketPagamentoApi.Services;

public sealed class PagamentoService : IPagamentoService
{
    private readonly IDbConnectionFactory _connectionFactory;
    private readonly ILogger<PagamentoService> _logger;

    public PagamentoService(
        IDbConnectionFactory connectionFactory,
        ILogger<PagamentoService> logger)
    {
        _connectionFactory = connectionFactory;
        _logger = logger;
    }

    public async Task<ResultadoProcessamento> ProcessarPagamentoAsync(
        PagamentoRequest request,
        CancellationToken ct)
    {
        using var connection = _connectionFactory.CreateConnection();
        if (connection.State != ConnectionState.Open)
        {
            connection.Open();
        }

        using var transaction = connection.BeginTransaction(IsolationLevel.Serializable);

        try
        {
            const string sqlInsertEvento = @"
                INSERT INTO eventos_processados (evento_id, pedido_id, valor, processado_em)
                VALUES (@EventoId, @PedidoId, @Valor, @ProcessadoEm);";

            await connection.ExecuteAsync(new CommandDefinition(
                sqlInsertEvento,
                new
                {
                    request.EventoId,
                    request.PedidoId,
                    request.Valor,
                    ProcessadoEm = DateTime.UtcNow.ToString("O")
                },
                transaction,
                cancellationToken: ct));

            const string sqlUpsertPedido = @"
                INSERT INTO pedidos (id, saldo_creditado, atualizado_em)
                VALUES (@PedidoId, @Valor, @AtualizadoEm)
                ON CONFLICT(id) DO UPDATE SET
                    saldo_creditado = pedidos.saldo_creditado + excluded.saldo_creditado,
                    atualizado_em = excluded.atualizado_em;";

            await connection.ExecuteAsync(new CommandDefinition(
                sqlUpsertPedido,
                new
                {
                    request.PedidoId,
                    request.Valor,
                    AtualizadoEm = DateTime.UtcNow.ToString("O")
                },
                transaction,
                cancellationToken: ct));

            transaction.Commit();

            _logger.LogInformation(
                "Pagamento creditado. Pedido: {PedidoId}, Evento: {EventoId}, Valor: {Valor}",
                request.PedidoId,
                request.EventoId,
                request.Valor);

            return ResultadoProcessamento.ProcessadoComSucesso;
        }
        catch (SqliteException ex) when (ex.SqliteErrorCode == 19)
        {
            transaction.Rollback();
            _logger.LogWarning(
                "Evento duplicado. EventoId: {EventoId}",
                request.EventoId);
            return ResultadoProcessamento.Duplicado;
        }
        catch (Exception ex)
        {
            transaction.Rollback();
            _logger.LogError(ex, "Falha ao processar pagamento do evento {EventoId}", request.EventoId);
            throw;
        }
    }
}