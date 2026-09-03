using Glorific.Api.Common;
using Glorific.Application.Exceptions;
using Glorific.Domain.Exceptions;

namespace Glorific.Api.Middleware;

/// <summary>
/// Traducao unica de excecao para HTTP. Nenhum controller deve ter try/catch de infraestrutura.
///
/// Duas correcoes em relacao ao repo de referencia estao aqui:
/// 1. UnauthorizedAccessException vira 401. La ela caia no default 500, e por isso dois
///    controllers ganharam try/catch local com um envelope de erro diferente do resto da API.
/// 2. BusinessValidationException propaga o detalhamento por campo em "errors", entao o front
///    consegue destacar o campo sem fazer parse da mensagem.
/// 3. IntegracaoNaoConfiguradaException vira 503 com uma mensagem que o cliente entende, em vez
///    do 500 generico. Integracao sem chave e problema NOSSO, e o 500 "informe o traceId ao
///    suporte" esconde exatamente a causa que resolveria o chamado em trinta segundos.
///
/// Ordem no pipeline importa: este middleware vem DEPOIS do UseCors. Se viesse antes, a resposta
/// de erro sairia sem o header Access-Control-Allow-Origin e o navegador mostraria "erro de CORS"
/// no lugar do 400 real — o erro vira invisivel para quem esta depurando.
/// </summary>
public sealed class ExceptionHandlingMiddleware
{
    private readonly RequestDelegate _proximo;
    private readonly ILogger<ExceptionHandlingMiddleware> _logger;

    public ExceptionHandlingMiddleware(RequestDelegate proximo, ILogger<ExceptionHandlingMiddleware> logger)
    {
        _proximo = proximo;
        _logger = logger;
    }

    public async Task InvokeAsync(HttpContext contexto)
    {
        try
        {
            await _proximo(contexto);
        }
        catch (OperationCanceledException) when (contexto.RequestAborted.IsCancellationRequested)
        {
            // O cliente desistiu (fechou a aba, timeout do axios). Nao e erro do servidor e nao
            // ha ninguem para receber resposta — logar como Error aqui polui o alerta de verdade.
            _logger.LogInformation(
                "Requisicao cancelada pelo cliente. {Metodo} {Caminho}",
                contexto.Request.Method,
                contexto.Request.Path);
        }
        catch (EntityNotFoundException excecao)
        {
            _logger.LogInformation(
                "Recurso nao encontrado. {Metodo} {Caminho}: {Mensagem}",
                contexto.Request.Method,
                contexto.Request.Path,
                excecao.Message);

            await RespostaErro.EscreverAsync(contexto, StatusCodes.Status404NotFound, excecao.Message);
        }
        catch (BusinessValidationException excecao)
        {
            // Culpa do input, nao do servidor: Warning, nunca Error, e sem stack trace no log.
            _logger.LogWarning(
                "Regra de negocio violada. {Metodo} {Caminho}: {Mensagem}",
                contexto.Request.Method,
                contexto.Request.Path,
                excecao.Message);

            await RespostaErro.EscreverAsync(
                contexto,
                StatusCodes.Status400BadRequest,
                excecao.Message,
                excecao.TemDetalhe ? excecao.Erros : null);
        }
        catch (IntegracaoNaoConfiguradaException excecao)
        {
            // Integracao CHAMADA sem estar configurada. Nao e culpa do input (400) e nao pode
            // virar o 500 generico: o 500 diz "informe o traceId ao suporte" e manda tres times
            // procurar um bug que nao existe, quando a causa e uma variavel de ambiente vazia.
            //
            // Error, e nao Warning: enquanto isso durar, um caminho inteiro de login esta fora do
            // ar para todos os clientes. Exige acao humana, e a mensagem tecnica com o nome da
            // chave fica AQUI, no log — nunca na resposta.
            _logger.LogError(
                "Integracao {Integracao} nao configurada. {Metodo} {Caminho}: {Mensagem}",
                excecao.Integracao,
                contexto.Request.Method,
                contexto.Request.Path,
                excecao.Message);

            // 503 e a leitura honesta: o servidor entendeu o pedido e nao tem como atende-lo
            // agora. 400 culparia o cliente por uma configuracao nossa.
            await RespostaErro.EscreverAsync(
                contexto,
                StatusCodes.Status503ServiceUnavailable,
                excecao.MensagemPublica);
        }
        catch (MelhorEnvioApiException excecao)
        {
            // Falha do PARCEIRO de frete. A traducao segue G.7 do blueprint e nao o status cru:
            // o status que vem aqui e o HTTP do Melhor Envio repassado pelo microservico, e um
            // 404 dele significa "conta nao conectada" (incidente nosso), nao "nao existe".
            if (excecao.EhContaNaoConectada)
            {
                // Critical de proposito: enquanto a conta estiver desautorizada a loja nao cota
                // nem despacha. E o unico erro desta familia que exige acao humana imediata.
                _logger.LogCritical(
                    excecao,
                    "Conta do Melhor Envio desconectada. Reautorize a integracao. {Metodo} {Caminho} TraceId={TraceId}",
                    contexto.Request.Method,
                    contexto.Request.Path,
                    RespostaErro.TraceId(contexto));

                await RespostaErro.EscreverAsync(
                    contexto,
                    StatusCodes.Status502BadGateway,
                    "Servico de frete indisponivel no momento. Tente novamente em instantes.");
            }
            else if (excecao.EhErroCliente)
            {
                // 4xx do parceiro e quase sempre dado nosso invalido (CEP fora de area, peso
                // zerado, endereco sem bairro). DetalheAmigavel corta o JSON cru do ME que o
                // microservico embute na mensagem — o cliente final nao pode ver aquilo.
                _logger.LogWarning(
                    "Frete recusado pelo parceiro ({Status}). {Metodo} {Caminho}: {Mensagem}",
                    excecao.StatusCode,
                    contexto.Request.Method,
                    contexto.Request.Path,
                    excecao.Message);

                await RespostaErro.EscreverAsync(
                    contexto,
                    StatusCodes.Status400BadRequest,
                    $"Frete: {excecao.DetalheAmigavel}");
            }
            else
            {
                // 5xx, timeout ou conexao recusada: e indisponibilidade, nao erro do cliente.
                // 502 e honesto e diz ao front que faz sentido tentar de novo.
                _logger.LogError(
                    excecao,
                    "Servico de frete indisponivel. {Metodo} {Caminho} TraceId={TraceId}",
                    contexto.Request.Method,
                    contexto.Request.Path,
                    RespostaErro.TraceId(contexto));

                await RespostaErro.EscreverAsync(
                    contexto,
                    StatusCodes.Status502BadGateway,
                    "Servico de frete indisponivel no momento. Tente novamente em instantes.");
            }
        }
        catch (UnauthorizedAccessException excecao)
        {
            _logger.LogWarning(
                "Acesso nao autorizado. {Metodo} {Caminho}: {Mensagem}",
                contexto.Request.Method,
                contexto.Request.Path,
                excecao.Message);

            // Mensagem generica de proposito: detalhar por que a credencial falhou entrega
            // informacao util para quem esta tentando adivinhar.
            await RespostaErro.EscreverAsync(
                contexto,
                StatusCodes.Status401Unauthorized,
                "Autenticacao necessaria ou credencial invalida.");
        }
        catch (Exception excecao)
        {
            _logger.LogError(
                excecao,
                "Erro nao tratado. {Metodo} {Caminho} TraceId={TraceId}",
                contexto.Request.Method,
                contexto.Request.Path,
                RespostaErro.TraceId(contexto));

            // NUNCA vaza excecao.Message no 500: mensagem de driver de banco expoe schema,
            // host e ate trecho de SQL. O traceId do envelope liga a resposta ao log completo.
            await RespostaErro.EscreverAsync(
                contexto,
                StatusCodes.Status500InternalServerError,
                "Ocorreu um erro inesperado. Informe o traceId ao suporte.");
        }
    }
}

/// <summary>Acucar para o Program.cs nao precisar do UseMiddleware generico solto.</summary>
public static class ExceptionHandlingMiddlewareExtensions
{
    public static IApplicationBuilder UseExceptionHandlingConfigurado(this IApplicationBuilder app) =>
        app.UseMiddleware<ExceptionHandlingMiddleware>();
}
