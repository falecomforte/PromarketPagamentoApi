using PromarketPagamentoApi.DTOs;

namespace PromarketPagamentoApi.Contracts;

public interface IPagamentoService
{
    Task<ResultadoProcessamento> ProcessarPagamentoAsync(PagamentoRequest request, CancellationToken ct);
}