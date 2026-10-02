# Promarket Pagamento API

API HTTP para registrar pagamentos e creditar valores ao saldo de pedidos. A aplicação usa SQLite para persistência e evita processar mais de uma vez um evento com o mesmo `EventoId`.

## Requisitos

- .NET 8 SDK

## Executar localmente

Na pasta do projeto, restaure as dependências e execute a aplicação:

```bash
dotnet restore
dotnet run --launch-profile PromarketPagamentoApi
```

O perfil local inicia a API em `http://localhost:5000`. No ambiente `Development`, a documentação Swagger fica disponível em `http://localhost:5000/swagger`.

Por padrão, o banco é criado como `pagamentos.db` na pasta de execução. A conexão pode ser substituída pela configuração `ConnectionStrings:DefaultConnection`; por variável de ambiente:

```powershell
$env:ConnectionStrings__DefaultConnection = "Data Source=pagamentos.db"
dotnet run --launch-profile PromarketPagamentoApi
```

Na inicialização, a aplicação cria as tabelas necessárias e aplica migrações para esquemas legados reconhecidos.

## Endpoint

### `POST /pagamentos`

Registra um evento de pagamento e soma seu valor ao saldo creditado do pedido.

Exemplo de requisição:

```http
POST http://localhost:5000/pagamentos
Content-Type: application/json

{
  "pedidoId": "pedido-123",
  "eventoId": "evento-456",
  "valor": 49.90
}
```

Exemplo com `curl`:

```bash
curl -X POST http://localhost:5000/pagamentos \
  -H "Content-Type: application/json" \
  -d '{"pedidoId":"pedido-123","eventoId":"evento-456","valor":49.90}'
```

Quando processado, retorna HTTP `200`:

```json
{
  "mensagem": "Pagamento processado com sucesso."
}
```

Se o `EventoId` já tiver sido registrado, o evento não é aplicado novamente e a API também retorna HTTP `200`, com a mensagem:

```json
{
  "mensagem": "Evento já processado anteriormente."
}
```

## Persistência e idempotência

- `eventos_processados` registra cada evento; `evento_id` é a chave primária.
- `pedidos` mantém o saldo creditado por pedido.
- O registro do evento e a atualização do saldo ocorrem na mesma transação SQLite.

## Segurança

A rota atualmente não configura autenticação ou autorização. Proteja o endpoint antes de disponibilizá-lo em um ambiente público.