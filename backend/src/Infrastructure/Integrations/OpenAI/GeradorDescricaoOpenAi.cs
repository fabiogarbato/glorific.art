using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json;
using Glorific.Application.Ports;
using Glorific.Application.Ports.Options;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace Glorific.Infrastructure.Integrations.OpenAI;

/// <summary>
/// Adaptador OpenAI das portas IGeradorDescricaoProduto e IGeradorTextoAlternativo. Mesmo
/// provedor e mesmo mecanismo (chat completions com entrada multimodal, imagem em data URI —
/// não depende da foto estar publicamente acessível, o que importa porque o storage do
/// catálogo às vezes só responde dentro da rede interna/tailnet), prompts diferentes: a
/// descrição de produto vende a peça, o alt text só descreve a foto pra quem não a vê.
/// </summary>
public sealed class GeradorDescricaoOpenAi :
    IGeradorDescricaoProduto,
    IGeradorTextoAlternativo,
    IGeradorNomeProduto,
    IGeradorSkuProduto
{
    private const string RotaChat = "/v1/chat/completions";

    private const string SistemaDescricao =
        "Você escreve descrições de produto para a Glorific (glorific.art), marca de camisetas " +
        "oversized streetwear com propósito cristão — tagline \"a arte de glorificar\". Tom: " +
        "direto, urbano, confiante, nunca piegas ou institucional demais. " +
        "Você recebe o nome da peça, a composição do tecido (se houver), a foto de capa do " +
        "produto e, quando disponíveis, descrições de outras peças já publicadas — use-as só " +
        "como referência de tom e estrutura, nunca copie frases delas. " +
        "O CENTRO do seu trabalho é INTERPRETAR a estampa, não catalogar o que está nela. " +
        "Toda estampa da Glorific carrega uma mensagem por trás dos elementos visuais — antes de " +
        "escrever, pare e pergunte: o que essa imagem está DIZENDO? Que tensão, contraste ou " +
        "verdade ela representa? Um versículo ao lado de uma cena não é ilustração solta: a " +
        "cena É a mensagem do versículo em forma visual, e é essa leitura que vai na descrição " +
        "— nunca liste \"tem um livro, tem prédios, tem um relógio\" como quem descreve uma " +
        "foto pra alguém cego; escreva como quem entendeu o que o artista quis dizer. " +
        "Só depois de capturar essa leitura é que a cor da peça e os detalhes técnicos entram, " +
        "como pé de página, não como abertura. " +
        "Nunca invente elemento que não está na imagem — a interpretação parte só do que você vê. " +
        "Escreva em português do Brasil, dois parágrafos curtos: o primeiro entrega a leitura da " +
        "estampa, com a força de quem já entendeu a peça antes de descrevê-la; o segundo fala de " +
        "modelagem, cor e cuidado com a peça. " +
        "Devolva SOMENTE o texto da descrição — sem título, sem aspas, sem markdown, sem listas.";

    private const string SistemaAltText =
        "Você escreve TEXTO ALTERNATIVO (atributo alt) de fotos de produto para a Glorific " +
        "(glorific.art), marca de camisetas oversized streetwear com propósito cristão. " +
        "Alt text não é descrição de venda: é uma frase curta e objetiva pra quem usa leitor de " +
        "tela e pra buscador de imagem entenderem o que a foto mostra. " +
        "Descreva o que está literalmente visível: a peça (cor, se é vista de frente/costas), a " +
        "estampa ou frase impressa se houver, e o enquadramento (still da peça, pessoa vestindo " +
        "etc.) — nunca invente o que não dá pra confirmar na imagem, e nunca escreva linguagem " +
        "de venda (\"linda\", \"perfeita\", \"compre já\"). " +
        "Quando receber exemplos de outras imagens do acervo, siga o MESMO padrão de estrutura e " +
        "nível de detalhe deles, sem copiar as frases. " +
        "Escreva em português do Brasil, uma frase só, sem ponto final, começando com o " +
        "substantivo principal (nunca \"Foto de\" ou \"Imagem de\" — isso é redundante pra quem " +
        "usa leitor de tela, que já anuncia \"imagem\" sozinho). " +
        "Devolva SOMENTE a frase — sem aspas, sem markdown.";

    private const string SistemaNome =
        "Você nomeia peças de roupa para a Glorific (glorific.art), marca de camisetas oversized " +
        "streetwear com propósito cristão — tagline \"a arte de glorificar\". " +
        "Você recebe a foto da peça e, quando disponíveis, nomes de outras peças já cadastradas " +
        "— use-os só como referência de formato e tamanho do nome, nunca copie. " +
        "Primeiro identifique O TIPO DA PEÇA pela foto (camiseta, boné, moletom, etc. — o que " +
        "estiver ali, sem presumir que é sempre camiseta). Depois, se houver estampa ou frase " +
        "impressa visível, capture o elemento mais forte dela — um símbolo, uma palavra, a ideia " +
        "central — e condense isso num nome curto e vendável, do jeito que uma loja de streetwear " +
        "nomeia produto: \"[Tipo da peça] [elemento ou conceito da estampa]\", sem vírgula, sem " +
        "explicar a estampa no nome. Nunca invente elemento que não está na imagem. " +
        "Escreva em português do Brasil, entre 2 e 6 palavras, capitalização de título. " +
        "Devolva SOMENTE o nome — sem aspas, sem markdown, sem explicação.";

    private const string SistemaSku =
        "Você gera o SKU base de peças de roupa para o catálogo interno da Glorific. Isto NÃO é " +
        "tarefa criativa: é reconhecer o PADRÃO de código já usado nos exemplos recebidos " +
        "(prefixo de tipo de peça, separadores, abreviação de palavras-chave do nome, uso ou não " +
        "de acentos/espaços) e aplicar o MESMO padrão ao nome da peça nova. Se não houver exemplo " +
        "nenhum, use maiúsculas, sem acento, palavras separadas por hífen, abreviando o tipo da " +
        "peça em 3-4 letras (ex.: camiseta → CAM, boné → BON, moletom → MOL) seguido das palavras " +
        "mais fortes do nome. " +
        "Devolva SOMENTE o código do SKU — sem aspas, sem markdown, sem explicação, sem espaços.";

    private readonly HttpClient _http;
    private readonly OpenAiOptions _opcoes;
    private readonly ILogger<GeradorDescricaoOpenAi> _logger;

    public GeradorDescricaoOpenAi(
        HttpClient http,
        IOptions<OpenAiOptions> opcoes,
        ILogger<GeradorDescricaoOpenAi> logger)
    {
        _http = http ?? throw new ArgumentNullException(nameof(http));
        _opcoes = opcoes?.Value ?? throw new ArgumentNullException(nameof(opcoes));
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));

        if (_http.BaseAddress is null && !string.IsNullOrWhiteSpace(_opcoes.BaseUrl))
            _http.BaseAddress = new Uri(_opcoes.BaseUrl.TrimEnd('/'), UriKind.Absolute);

        if (string.IsNullOrWhiteSpace(_opcoes.ApiKey))
        {
            _logger.LogWarning(
                "OpenAI:ApiKey nao configurada. Gerar texto com IA vai falhar ate a chave ser definida.");
        }
        else if (_http.DefaultRequestHeaders.Authorization is null)
        {
            _http.DefaultRequestHeaders.Authorization =
                new AuthenticationHeaderValue("Bearer", _opcoes.ApiKey.Trim());
        }
    }

    public Task<string> GerarAsync(DescricaoProdutoPedido pedido, CancellationToken ct = default)
    {
        ArgumentNullException.ThrowIfNull(pedido);

        var linhas = new List<string> { $"Nome da peça: {pedido.NomeProduto}" };

        if (!string.IsNullOrWhiteSpace(pedido.ComposicaoTecido))
            linhas.Add($"Composição do tecido: {pedido.ComposicaoTecido}");

        if (pedido.DescricoesExemplo.Count > 0)
        {
            linhas.Add("");
            linhas.Add("Descrições de outras peças da loja, só como referência de tom e estrutura:");
            AdicionarExemplosNumerados(linhas, pedido.DescricoesExemplo);
        }

        linhas.Add("");
        linhas.Add("Escreva a descrição desta peça agora, olhando a imagem anexada.");

        return ChamarAsync(SistemaDescricao, string.Join('\n', linhas), pedido.ImagemBytes, pedido.ImagemContentType, ct);
    }

    public Task<string> GerarAsync(TextoAlternativoPedido pedido, CancellationToken ct = default)
    {
        ArgumentNullException.ThrowIfNull(pedido);

        var linhas = new List<string>();

        if (pedido.ExemplosExistentes.Count > 0)
        {
            linhas.Add("Texto alternativo de outras imagens do acervo, só como referência de padrão:");
            AdicionarExemplosNumerados(linhas, pedido.ExemplosExistentes);
            linhas.Add("");
        }

        linhas.Add("Escreva o texto alternativo desta imagem agora.");

        return ChamarAsync(SistemaAltText, string.Join('\n', linhas), pedido.ImagemBytes, pedido.ImagemContentType, ct);
    }

    public Task<string> GerarAsync(NomeProdutoPedido pedido, CancellationToken ct = default)
    {
        ArgumentNullException.ThrowIfNull(pedido);

        var linhas = new List<string>();

        if (!string.IsNullOrWhiteSpace(pedido.CategoriaNome))
            linhas.Add($"Categoria cadastrada da peça: {pedido.CategoriaNome}");

        if (pedido.NomesExemplo.Count > 0)
        {
            linhas.Add("Nomes de outras peças da loja, só como referência de formato:");
            AdicionarExemplosNumerados(linhas, pedido.NomesExemplo);
            linhas.Add("");
        }

        linhas.Add("Nomeie esta peça agora, olhando a imagem anexada.");

        return ChamarAsync(SistemaNome, string.Join('\n', linhas), pedido.ImagemBytes, pedido.ImagemContentType, ct);
    }

    public Task<string> GerarAsync(SkuProdutoPedido pedido, CancellationToken ct = default)
    {
        ArgumentNullException.ThrowIfNull(pedido);

        var linhas = new List<string> { $"Nome da peça: {pedido.NomeProduto}" };

        if (!string.IsNullOrWhiteSpace(pedido.CategoriaNome))
            linhas.Add($"Categoria: {pedido.CategoriaNome}");

        if (pedido.ExemplosSku.Count > 0)
        {
            linhas.Add("");
            linhas.Add("Nome → SKU de outras peças já cadastradas, para você seguir o mesmo padrão:");
            AdicionarExemplosNumerados(linhas, pedido.ExemplosSku);
        }

        linhas.Add("");
        linhas.Add("Gere o SKU base desta peça agora.");

        return ChamarTextoAsync(SistemaSku, string.Join('\n', linhas), ct);
    }

    private static void AdicionarExemplosNumerados(List<string> linhas, IReadOnlyList<string> exemplos)
    {
        var indice = 1;
        foreach (var exemplo in exemplos)
        {
            linhas.Add($"{indice}) {exemplo}");
            indice++;
        }
    }

    private Task<string> ChamarAsync(
        string sistema,
        string textoUsuario,
        byte[] imagemBytes,
        string imagemContentType,
        CancellationToken ct)
    {
        var dataUri = $"data:{imagemContentType};base64,{Convert.ToBase64String(imagemBytes)}";

        object[] conteudoUsuario =
        [
            new { type = "text", text = textoUsuario },
            new { type = "image_url", image_url = new { url = dataUri } },
        ];

        return ChamarCompletionAsync(sistema, conteudoUsuario, ct);
    }

    /// <summary>Mesma chamada, sem imagem — usada por tarefas de texto puro como o SKU.</summary>
    private Task<string> ChamarTextoAsync(string sistema, string textoUsuario, CancellationToken ct)
    {
        object[] conteudoUsuario = [new { type = "text", text = textoUsuario }];
        return ChamarCompletionAsync(sistema, conteudoUsuario, ct);
    }

    private async Task<string> ChamarCompletionAsync(string sistema, object[] conteudoUsuario, CancellationToken ct)
    {
        if (string.IsNullOrWhiteSpace(_opcoes.ApiKey))
            throw new InvalidOperationException("OpenAI:ApiKey nao configurada.");

        var corpo = new
        {
            model = _opcoes.Modelo,
            temperature = 0.7,
            max_tokens = 500,
            messages = new object[]
            {
                new { role = "system", content = sistema },
                new { role = "user", content = conteudoUsuario },
            },
        };

        using var resposta = await _http.PostAsJsonAsync(RotaChat, corpo, ct);
        var corpoResposta = await resposta.Content.ReadAsStringAsync(ct);

        if (!resposta.IsSuccessStatusCode)
        {
            _logger.LogWarning(
                "OpenAI respondeu {Status} ao gerar texto. Corpo={Corpo}",
                (int)resposta.StatusCode,
                corpoResposta);

            throw new InvalidOperationException(
                $"O gerador de texto não respondeu corretamente (HTTP {(int)resposta.StatusCode}).");
        }

        var texto = ExtrairTexto(corpoResposta);

        if (string.IsNullOrWhiteSpace(texto))
            throw new InvalidOperationException("O gerador de texto devolveu uma resposta vazia.");

        return texto.Trim();
    }

    private static string? ExtrairTexto(string corpoJson)
    {
        using var documento = JsonDocument.Parse(corpoJson);

        return documento.RootElement
            .GetProperty("choices")[0]
            .GetProperty("message")
            .GetProperty("content")
            .GetString();
    }
}
