using System.Data;
using Microsoft.Data.Sqlite;
using Microsoft.Extensions.Logging.Abstractions;
using Moq;
using PromarketPagamentoApi.Contracts;
using PromarketPagamentoApi.DTOs;
using PromarketPagamentoApi.Services;
using Dapper;

namespace PromarketPagamentoApi.Tests;

[TestClass]
public class PagamentoServiceTests
{
    private SqliteConnection _connection;
    private Mock<IDbConnectionFactory> _connectionFactoryMock;
    private PagamentoService _service;

    [TestInitialize]
    public void Setup()
    {
        // Usa um banco de dados em memória que persiste enquanto a conexão estiver aberta.
        _connection = new SqliteConnection("DataSource=:memory:");
        _connection.Open();

        // Cria as tabelas necessárias para os testes
        _connection.Execute(@"
            CREATE TABLE pedidos (
                id TEXT PRIMARY KEY,
                saldo_creditado REAL NOT NULL,
                atualizado_em TEXT NOT NULL
            );

            CREATE TABLE eventos_processados (
                evento_id TEXT PRIMARY KEY,
                pedido_id TEXT NOT NULL,
                valor REAL NOT NULL,
                processado_em TEXT NOT NULL
            );
        ");

        _connectionFactoryMock = new Mock<IDbConnectionFactory>();
        _connectionFactoryMock.Setup(f => f.CreateConnection()).Returns(_connection);

        var logger = NullLogger<PagamentoService>.Instance;

        _service = new PagamentoService(_connectionFactoryMock.Object, logger);
    }

    [TestCleanup]
    public void Cleanup()
    {
        _connection.Dispose();
    }

    [TestMethod]
    public async Task ProcessarPagamentoAsync_DeveProcessarComSucesso_E_CreditarPedido()
    {
        // Arrange
        var request = new PagamentoRequest(
            PedidoId: Guid.NewGuid().ToString(),
            EventoId: Guid.NewGuid().ToString(),
            Valor: 150.50m
        );

        // Act
        var resultado = await _service.ProcessarPagamentoAsync(request, CancellationToken.None);

        // Assert
        Assert.AreEqual(ResultadoProcessamento.ProcessadoComSucesso, resultado);

        var saldoPedido = _connection.QuerySingle<decimal>(
            "SELECT saldo_creditado FROM pedidos WHERE id = @PedidoId", 
            new { request.PedidoId });
            
        Assert.AreEqual(150.50m, saldoPedido);
    }

    [TestMethod]
    public async Task ProcessarPagamentoAsync_EventoDuplicado_DeveRetornarDuplicado()
    {
        // Arrange
        var request = new PagamentoRequest(
            PedidoId: Guid.NewGuid().ToString(),
            EventoId: Guid.NewGuid().ToString(),
            Valor: 100.00m
        );

        // Processa a primeira vez
        await _service.ProcessarPagamentoAsync(request, CancellationToken.None);

        // Act
        var resultado = await _service.ProcessarPagamentoAsync(request, CancellationToken.None);

        // Assert
        Assert.AreEqual(ResultadoProcessamento.Duplicado, resultado);

        // Verifica que o saldo não foi creditado duas vezes
        var saldoPedido = _connection.QuerySingle<decimal>(
            "SELECT saldo_creditado FROM pedidos WHERE id = @PedidoId", 
            new { request.PedidoId });
            
        Assert.AreEqual(100.00m, saldoPedido);
    }

    [TestMethod]
    public async Task ProcessarPagamentoAsync_MultiplosEventosParaMesmoPedido_DeveSomarValores()
    {
        // Arrange
        var pedidoId = Guid.NewGuid().ToString();
        var request1 = new PagamentoRequest(
            PedidoId: pedidoId,
            EventoId: Guid.NewGuid().ToString(),
            Valor: 100.00m
        );

        var request2 = new PagamentoRequest(
            PedidoId: pedidoId,
            EventoId: Guid.NewGuid().ToString(),
            Valor: 50.00m
        );

        // Act
        await _service.ProcessarPagamentoAsync(request1, CancellationToken.None);
        await _service.ProcessarPagamentoAsync(request2, CancellationToken.None);

        // Assert
        var saldoPedido = _connection.QuerySingle<decimal>(
            "SELECT saldo_creditado FROM pedidos WHERE id = @PedidoId", 
            new { PedidoId = pedidoId });
            
        Assert.AreEqual(150.00m, saldoPedido);
    }
}
