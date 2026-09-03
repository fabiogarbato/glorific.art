# glorific.art

Ecommerce de moda cristã. Vitrine editorial em paleta off-white, painel administrativo
completo, login com Google, frete pelo Melhor Envio e pagamento pela InfinitePay.

## Stack

| Camada | Tecnologia |
|---|---|
| Backend | .NET 10 · Clean Architecture · EF Core 10 · PostgreSQL 16 |
| Frontend | React 19 · Vite · Tailwind 3 · DaisyUI · React Query v5 |
| Frete | Melhor Envio, via microserviço próprio (container separado) |
| Pagamento | InfinitePay Checkout Web |
| Identidade | JWT próprio (access 15 min + refresh rotativo) · Google Sign-In · e-mail/senha |

## Arquitetura

Dependências apontam sempre para dentro:

```
Domain            → nada
Application       → Domain
Infrastructure    → Domain, Application      (implementa as portas)
API               → Application, Infrastructure
```

- Interfaces de **repositório** vivem em `Domain/Interfaces/Repositories/`.
- Interfaces de **integração** (Melhor Envio, pagamento, Google, e-mail, storage) vivem em
  `Application/Ports/`, com modelos próprios de fronteira — nenhum tipo de Infrastructure
  atravessa a porta.
- Repositório **nunca** chama `SaveChanges`. Quem salva é o caso de uso, via `IUnitOfWork`.

## Subindo local

```bash
cp .env.example .env
```

Preencha o `.env` (JWT, Google Client ID, handle da InfinitePay, chave do Melhor Envio).

A rede do Melhor Envio é externa e precisa existir **uma vez por host**:

```bash
docker network create --driver bridge --subnet 172.27.0.0/16 --gateway 172.27.0.1 GLORIFIC_ME
```

```bash
docker compose up --build
```

| Serviço | URL |
|---|---|
| Loja | http://localhost:5174 |
| API | http://localhost:5080 |
| Swagger | http://localhost:5080/swagger (fora de produção) |
| Postgres | localhost:5433 |

### Sem Docker

```bash
docker compose up -d db
```

```bash
dotnet run --project backend/src/API
```

```bash
npm --prefix frontend run dev
```

## Decisões que valem saber

- **Dinheiro é sempre `int` em centavos.** Nunca `decimal` de reais em coluna de preço.
- **Estoque é por SKU** (`produto_variacoes`), nunca por produto. Reserva é *soft*:
  o checkout incrementa `quantidade_reservada` e `quantidade` continua sendo o estoque físico.
- **Item de pedido é imutável e autossuficiente.** Nome, tamanho, cor, foto e preço são
  snapshot do instante da compra — renomear um produto não pode reescrever recibo antigo.
- **Peso e dimensão são obrigatórios na variação.** Sem eles não existe cotação no Melhor Envio,
  e "P" e "GG" têm peso diferente.
- **Pagamento só vira PAGO depois de conferir na InfinitePay.** O webhook dela não é assinado;
  confirmamos via `payment_check` e comparamos o valor com o total do pedido.
- **Zero `DateTime.Now`.** Sempre `IClock.UtcNow`.

## Testes

```bash
dotnet test backend/glorific.slnx
```

```bash
npm --prefix frontend run test
```
