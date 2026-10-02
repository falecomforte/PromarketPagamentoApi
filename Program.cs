using PromarketPagamentoApi.Contracts;
using PromarketPagamentoApi.Data;
using PromarketPagamentoApi.DTOs;
using PromarketPagamentoApi.Services;

var builder = WebApplication.CreateBuilder(args);

builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen();

var connectionString = builder.Configuration.GetConnectionString("DefaultConnection") ?? "Data Source=pagamentos.db";
builder.Services.AddSingleton<IDbConnectionFactory>(_ => new SqliteConnectionFactory(connectionString));
builder.Services.AddScoped<IPagamentoService, PagamentoService>();

var app = builder.Build();

await DatabaseSetup.InicializarBancoAsync(app.Services.GetRequiredService<IDbConnectionFactory>());

if (app.Environment.IsDevelopment())
{
    app.UseSwagger();
    app.UseSwaggerUI();
}

app.MapPost("/pagamentos", async (
    PagamentoRequest request,
    IPagamentoService pagamentoService,
    CancellationToken cancellationToken) =>
{
    var resultado = await pagamentoService.ProcessarPagamentoAsync(request, cancellationToken);

    return resultado switch
    {
        ResultadoProcessamento.ProcessadoComSucesso => Results.Ok(new
        {
            Mensagem = "Pagamento processado com sucesso."
        }),
        ResultadoProcessamento.Duplicado => Results.Ok(new
        {
            Mensagem = "Evento já processado anteriormente."
        }),
        _ => Results.Problem("Resultado de processamento desconhecido.")
    };
})
    .WithSummary("Processa um pagamento")
    .WithDescription("Credita o valor ao pedido e ignora eventos já processados.");

app.Run();

public partial class Program { }

