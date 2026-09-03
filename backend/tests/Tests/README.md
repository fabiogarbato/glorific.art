# Testes — Glorific

Suite unica (`Glorific.Tests`), xUnit puro, sem biblioteca de mock. Todo test double e
escrito a mao (ex.: `CapturingHandler : HttpMessageHandler`).

## Pre-requisito: Docker

**Docker Desktop precisa estar rodando.** Os testes de banco e de API sobem um Postgres
REAL via Testcontainers (`postgres:16-alpine`, a mesma imagem do `docker-compose.yml`).

Nao usamos SQLite de proposito: SQLite nao valida `jsonb`, `xmin`, `timestamp without time
zone`, indice parcial nem `ON DELETE` — que e exatamente onde este schema precisa ser testado.

Testcontainers escolhe portas dinamicas, entao o Postgres do `docker-compose` (5433) pode ficar
de pe ao mesmo tempo, sem conflito. Nao e preciso subir nada a mao: o container e criado e
destruido pela propria suite.

## Rodar

```bash
cd backend
dotnet test glorific.slnx
```

Suite inteira: **621 testes, ~25 s**.

Se o build falhar com "arquivo bloqueado por Glorific.Api", tem uma API de dev de pe segurando
os DLLs — encerre o processo `Glorific.Api.exe` antes de buildar.

### Recortes uteis

```bash
# So o que nao precisa de Docker (461 testes, ~6 s)
dotnet test glorific.slnx --filter "FullyQualifiedName~Dominio|FullyQualifiedName~Seguranca|FullyQualifiedName~Integracoes"

# So persistencia (Postgres real)
dotnet test glorific.slnx --filter "FullyQualifiedName~Persistencia"

# So API HTTP ponta a ponta
dotnet test glorific.slnx --filter "FullyQualifiedName~Glorific.Tests.Api"

# Uma classe
dotnet test glorific.slnx --filter "FullyQualifiedName~EstoqueConcorrencia"
```

## Como a suite e organizada

| Pasta | Precisa de Docker | O que cobre |
|---|---|---|
| `Dominio/` | nao | Helpers puros: documento, telefone, CEP, slug, peso cubado, retry de envio |
| `Seguranca/` | nao | `CorsOriginMatcher` e hashing de senha (BCrypt) |
| `Integracoes/` | nao | Adaptadores HTTP (Melhor Envio, InfinitePay, Google) contra `CapturingHandler` — **nenhum teste vai a rede** |
| `Persistencia/` | **sim** | Concorrencia de estoque e cupom, idempotencia de pagamento, CHECKs/indices/FKs do schema, soft delete |
| `Api/` | **sim** | `WebApplicationFactory` + Postgres real: matriz de autorizacao, envelope de erro, rotas publicas, fluxo de carrinho |

### Duas fixtures, dois containers

- `BancoFixture` / colecao `"banco-postgres"` — contexto EF + SQL cru, para schema e concorrencia.
- `ApiFixture` / colecao `"api-http-testcontainers"` — sobe a API inteira sobre o mesmo tipo de container.

Cada uma sobe **UM** container por sessao (`ICollectionFixture`), nao um por classe.

`ApiFixture` roda com `DisableParallelization = true`: a configuracao dela viaja por variavel de
ambiente (o `Program.cs` le config antes do `builder.Build()`), e variavel de ambiente e estado
de processo — duas colecoes subindo API em paralelo disputariam a mesma connection string.

### Isolamento entre testes

`BancoFixture.LimparAsync()` faz `TRUNCATE ... RESTART IDENTITY CASCADE` em todas as tabelas do
schema `public` (menos `__EFMigrationsHistory`) e re-executa o `SeedInicial`.

Nao usamos transacao revertida de proposito: ela obrigaria o teste inteiro a viver numa conexao
so, e os testes de concorrencia precisam de conexoes separadas para o UPDATE condicional disputar
a linha de verdade. Dentro de uma transacao compartilhada, o teste de oversell nao provaria nada.

O schema vem do `MigrationRunner` de producao, nao de `EnsureCreated` — `EnsureCreated` pularia as
migrations e esconderia divergencia entre migration e modelo.

## Regras da casa

- Nome de teste em portugues, formato `Metodo_Situacao_ResultadoEsperado`.
- Todo teste e deterministico e **nao depende de ordem** — cada classe passa sozinha e a suite
  passa repetida.
- Nenhum teste acessa a rede. Integracao externa e sempre via handler capturado.
- Se um teste ficar vermelho por bug no codigo de producao, **conserte a producao**. Afrouxar a
  assercao para o verde e pior que nao ter o teste.
