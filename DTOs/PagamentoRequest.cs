namespace PromarketPagamentoApi.DTOs;

public sealed record PagamentoRequest(string PedidoId, string EventoId, decimal Valor);