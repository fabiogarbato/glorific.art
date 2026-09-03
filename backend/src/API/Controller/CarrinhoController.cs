using System.Security.Claims;
using Glorific.Api.Configuration;
using Glorific.Application.DTO.Carrinho;
using Glorific.Application.DTO.Frete;
using Glorific.Application.Exceptions;
using Glorific.Application.Services.Interfaces;
using Glorific.Domain.Interfaces;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.RateLimiting;

namespace Glorific.Api.Controller;

/// <summary>
/// Carrinho da loja. Publico por natureza: o visitante monta o carrinho ANTES de ter conta, e
/// exigir login para isso e o jeito mais eficiente de perder a venda.
///
/// A identidade sai de duas fontes, nesta ordem: a claim sub do token (cliente logado) e, na
/// falta dela, o cookie de sessao gl_cart. NENHUM endpoint aqui aceita id de carrinho vindo do
/// cliente — seria entregar o carrinho de qualquer pessoa a quem chutasse o valor.
///
/// O cookie e httpOnly e restrito ao caminho deste controller: e um identificador opaco de
/// sessao, o front nao tem motivo para le-lo em JavaScript e reduzir o alcance limita o estrago
/// de um XSS em outra pagina do site.
///
/// AUTORIZACAO: [Authorize] na CLASSE e [AllowAnonymous] EXPLICITO em cada rota publica, e nao o
/// contrario. Com [AllowAnonymous] na classe o [Authorize] de "merge" era engolido (ASP0026): o
/// atributo mais distante vence, e a rota que funde carrinhos ficava aberta a visitante. Nesta
/// ordem, esquecer o atributo numa rota nova falha fechado — 401 barulhento em vez de rota de
/// carrinho autenticada exposta em silencio.
/// </summary>
[ApiController]
[Route("api/v1/carrinho")]
[Authorize]
[Produces("application/json")]
public sealed class CarrinhoController : ControllerBase
{
    private const string CookieSessao = "gl_cart";

    /// <summary>Escopo minimo: so as rotas de carrinho precisam enxergar a sessao anonima.</summary>
    private const string CaminhoCookie = "/api/v1/carrinho";

    /// <summary>Casa com o prazo de validade do carrinho no servico.</summary>
    private const int DiasCookie = 30;

    /// <summary>Guid "N": 32 caracteres hexadecimais.</summary>
    private const int TamanhoChaveSessao = 32;

    private readonly ICarrinhoService _carrinhos;
    private readonly IFreteService _fretes;
    private readonly IClock _relogio;

    public CarrinhoController(ICarrinhoService carrinhos, IFreteService fretes, IClock relogio)
    {
        _carrinhos = carrinhos ?? throw new ArgumentNullException(nameof(carrinhos));
        _fretes = fretes ?? throw new ArgumentNullException(nameof(fretes));

        // IClock ate para carimbar a expiracao do cookie: regra dura do projeto, zero "agora"
        // fora do relogio injetado. Um host em UTC-3 encurtaria a validade do cookie em 3 h.
        _relogio = relogio ?? throw new ArgumentNullException(nameof(relogio));
    }

    /// <summary>Carrinho atual. Nao cria nada: sem carrinho, devolve um carrinho vazio.</summary>
    [HttpGet]
    [AllowAnonymous]
    [ProducesResponseType(StatusCodes.Status200OK)]
    public async Task<ActionResult<CarrinhoResponseDto>> Obter(CancellationToken cancellationToken)
    {
        // criarSessao: false — leitura de robo de indexacao nao precisa ganhar cookie nem linha.
        var identidade = ResolverIdentidade(criarSessao: false);

        return Ok(await _carrinhos.ObterAsync(identidade, cancellationToken));
    }

    /// <summary>Adiciona item (ou soma a quantidade quando a variacao ja esta no carrinho).</summary>
    [HttpPost("itens")]
    [AllowAnonymous]
    [ProducesResponseType(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    public async Task<ActionResult<CarrinhoResponseDto>> AdicionarItem(
        [FromBody] CarrinhoItemCreateDto dto,
        CancellationToken cancellationToken)
    {
        var identidade = ResolverIdentidade(criarSessao: true);

        return Ok(await _carrinhos.AdicionarItemAsync(identidade, dto, cancellationToken));
    }

    /// <summary>Define a quantidade da linha. Zero remove o item.</summary>
    [HttpPatch("itens/{idItem:int}")]
    [AllowAnonymous]
    [ProducesResponseType(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    public async Task<ActionResult<CarrinhoResponseDto>> AlterarItem(
        int idItem,
        [FromBody] CarrinhoItemUpdateDto dto,
        CancellationToken cancellationToken)
    {
        var identidade = ResolverIdentidade(criarSessao: false);

        return Ok(await _carrinhos.AlterarQuantidadeAsync(identidade, idItem, dto, cancellationToken));
    }

    [HttpDelete("itens/{idItem:int}")]
    [AllowAnonymous]
    [ProducesResponseType(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    public async Task<ActionResult<CarrinhoResponseDto>> RemoverItem(
        int idItem,
        CancellationToken cancellationToken)
    {
        var identidade = ResolverIdentidade(criarSessao: false);

        return Ok(await _carrinhos.RemoverItemAsync(identidade, idItem, cancellationToken));
    }

    /// <summary>Esvazia o carrinho e solta o cupom aplicado.</summary>
    [HttpDelete]
    [AllowAnonymous]
    [ProducesResponseType(StatusCodes.Status200OK)]
    public async Task<ActionResult<CarrinhoResponseDto>> Esvaziar(CancellationToken cancellationToken)
    {
        var identidade = ResolverIdentidade(criarSessao: false);

        return Ok(await _carrinhos.EsvaziarAsync(identidade, cancellationToken));
    }

    /// <summary>
    /// Funde o carrinho anonimo no do usuario. Chamado pelo front logo depois do login Google.
    ///
    /// Unica rota do controller SEM [AllowAnonymous]: herda o [Authorize] da classe. E
    /// le a chave anonima do cookie — nunca do corpo, senao qualquer pessoa poderia mandar a
    /// chave de sessao de outra e absorver o carrinho dela.
    /// </summary>
    [HttpPost("merge")]
    [Authorize]
    [ProducesResponseType(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    public async Task<ActionResult<CarrinhoResponseDto>> Mesclar(CancellationToken cancellationToken)
    {
        var uuid = UuidUsuario();

        if (string.IsNullOrWhiteSpace(uuid))
            return Unauthorized();

        var chaveAnonima = ChaveSessaoDoCookie();

        var resultado = await _carrinhos.MesclarAsync(uuid, chaveAnonima, cancellationToken);

        // O carrinho anonimo deixou de existir como "aberto": manter o cookie faria o proximo
        // logout devolver um carrinho fantasma que nao existe mais no banco.
        if (!string.IsNullOrWhiteSpace(chaveAnonima))
            Response.Cookies.Delete(CookieSessao, new CookieOptions { Path = CaminhoCookie });

        return Ok(resultado);
    }

    /// <summary>Aplica cupom. Previa de desconto: quem cobra e valida de verdade e o checkout.</summary>
    [HttpPost("cupom")]
    [AllowAnonymous]
    [ProducesResponseType(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    public async Task<ActionResult<CarrinhoResponseDto>> AplicarCupom(
        [FromBody] CupomAplicacaoDto dto,
        CancellationToken cancellationToken)
    {
        var identidade = ResolverIdentidade(criarSessao: false);

        return Ok(await _carrinhos.AplicarCupomAsync(identidade, dto, cancellationToken));
    }

    [HttpDelete("cupom")]
    [AllowAnonymous]
    [ProducesResponseType(StatusCodes.Status200OK)]
    public async Task<ActionResult<CarrinhoResponseDto>> RemoverCupom(CancellationToken cancellationToken)
    {
        var identidade = ResolverIdentidade(criarSessao: false);

        return Ok(await _carrinhos.RemoverCupomAsync(identidade, cancellationToken));
    }

    /// <summary>
    /// Simulador de frete da tela do carrinho.
    ///
    /// Os itens saem do carrinho do SERVIDOR; do cliente vem so o CEP. Com rate limit proprio
    /// porque cada chamada vira uma consulta paga no Melhor Envio — um bot cotando em laco nao
    /// derruba a loja, mas queima a cota da conta.
    /// </summary>
    [HttpPost("frete")]
    [AllowAnonymous]
    [EnableRateLimiting(PoliticasRateLimit.Frete)]
    [ProducesResponseType(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status429TooManyRequests)]
    public async Task<ActionResult<IReadOnlyList<OpcaoFreteResponseDto>>> CotarFrete(
        [FromBody] CotacaoCarrinhoRequestDto dto,
        CancellationToken cancellationToken)
    {
        var identidade = ResolverIdentidade(criarSessao: false);

        var carrinho = await _carrinhos.ObterAsync(identidade, cancellationToken);

        if (carrinho.Itens.Count == 0)
            throw new BusinessValidationException("Adicione itens ao carrinho para calcular o frete.");

        var itens = carrinho.Itens
            .Select(i => new ItemCotacaoDto { IdVariacao = i.IdVariacao, Quantidade = i.Quantidade })
            .ToArray();

        return Ok(await _fretes.CotarItensAsync(dto.Cep, itens, cancellationToken));
    }

    // ------------------------------------------------------------------
    // Identidade
    // ------------------------------------------------------------------

    /// <summary>
    /// Resolve quem e o dono do carrinho nesta requisicao.
    ///
    /// Usuario autenticado tem precedencia sobre o cookie: depois do login o cookie antigo
    /// continua no navegador, e deixar ele ganhar faria o cliente logado ver o carrinho do
    /// visitante que ele era.
    ///
    /// <paramref name="criarSessao"/> so e true nas acoes que de fato criam carrinho. Emitir
    /// cookie em toda leitura marcaria cada robo de indexacao com uma sessao.
    /// </summary>
    private IdentidadeCarrinho ResolverIdentidade(bool criarSessao)
    {
        var uuid = UuidUsuario();

        if (!string.IsNullOrWhiteSpace(uuid))
            return new IdentidadeCarrinho { UuidUsuario = uuid };

        var chave = ChaveSessaoDoCookie();

        if (string.IsNullOrWhiteSpace(chave) && criarSessao)
        {
            chave = Guid.NewGuid().ToString("N");
            GravarCookie(chave);
        }

        return new IdentidadeCarrinho { ChaveSessao = chave };
    }

    private string? UuidUsuario() =>
        User.Identity?.IsAuthenticated == true
            ? User.FindFirstValue(AutenticacaoConfiguration.ClaimSub)
            : null;

    /// <summary>
    /// Le a chave do cookie ACEITANDO apenas o formato que nos mesmos emitimos.
    ///
    /// A chave vai direto para um WHERE por igualdade, entao nao ha risco de injecao — mas sem
    /// esta guarda um cliente poderia gravar uma chave de 8 KB no cookie e transformar cada
    /// consulta de carrinho numa comparacao de string gigante, de graca.
    /// </summary>
    private string? ChaveSessaoDoCookie()
    {
        if (!Request.Cookies.TryGetValue(CookieSessao, out var valor))
            return null;

        if (string.IsNullOrWhiteSpace(valor) || valor.Length != TamanhoChaveSessao)
            return null;

        foreach (var caractere in valor)
        {
            var hexadecimal = caractere is >= '0' and <= '9'
                              || caractere is >= 'a' and <= 'f'
                              || caractere is >= 'A' and <= 'F';

            if (!hexadecimal)
                return null;
        }

        return valor;
    }

    private void GravarCookie(string chave) =>
        Response.Cookies.Append(CookieSessao, chave, new CookieOptions
        {
            HttpOnly = true,
            Path = CaminhoCookie,
            Expires = new DateTimeOffset(
                DateTime.SpecifyKind(_relogio.UtcNow, DateTimeKind.Utc)).AddDays(DiasCookie),
            IsEssential = true,

            // Em HTTPS o cookie precisa de SameSite=None para sobreviver ao front em outro
            // dominio (o CORS do projeto ja permite credenciais). Em HTTP local, None exige
            // Secure e o navegador DESCARTA o cookie — por isso o par varia com o esquema.
            Secure = Request.IsHttps,
            SameSite = Request.IsHttps ? SameSiteMode.None : SameSiteMode.Lax
        });
}
