using Glorific.Application.Models.Midia;
using Glorific.Application.Ports;
using Glorific.Domain.Helpers;
using Glorific.Domain.Interfaces;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace Glorific.Infrastructure.Storage;

/// <summary>
/// IImageStorage gravando no disco local, servido como arquivo estatico (wwwroot/media).
///
/// E o suficiente para o MVP e a porta permite trocar por Cloudinary/S3 depois sem tocar em
/// nenhum servico da Application. As decisoes que valem o comentario:
///
/// 1. O NOME do arquivo e um Guid, nunca o nome enviado. Nome de upload e entrada hostil:
///    "../../appsettings.json" e path traversal, e nome repetido sobrescreve foto de outro
///    produto. A extensao sai do CONTENT-TYPE conferido, nao do que veio no nome.
///
/// 2. O PublicId e o caminho relativo (media/2026/09/&lt;guid&gt;.jpg) e RemoverAsync so aceita
///    caminho que continue dentro da raiz depois de resolvido — sem essa checagem, um PublicId
///    manipulado apagaria arquivo fora da pasta de midia.
///
/// 3. Particionamento por ano/mes. Dezenas de milhares de arquivos numa pasta so degradam
///    listagem e backup em qualquer sistema de arquivos.
/// </summary>
public sealed class ArmazenamentoLocalImagem : IImageStorage
{
    private readonly ArmazenamentoLocalOptions _opcoes;
    private readonly IClock _relogio;
    private readonly ILogger<ArmazenamentoLocalImagem> _logger;

    public ArmazenamentoLocalImagem(
        IOptions<ArmazenamentoLocalOptions> opcoes,
        IClock relogio,
        ILogger<ArmazenamentoLocalImagem> logger)
    {
        _opcoes = opcoes?.Value ?? new ArmazenamentoLocalOptions();
        _relogio = relogio ?? throw new ArgumentNullException(nameof(relogio));
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
    }

    /// <inheritdoc />
    public async Task<ImagemArmazenadaInfo> EnviarAsync(
        Stream conteudo,
        string nomeArquivo,
        string contentType,
        CancellationToken ct = default)
    {
        ArgumentNullException.ThrowIfNull(conteudo);

        if (string.IsNullOrWhiteSpace(contentType) || !ContentTypePermitido(contentType))
            throw new InvalidOperationException($"Content-type nao suportado pelo armazenamento: '{contentType}'.");

        // Buffer em memoria por dois motivos: o cabecalho precisa ser lido para extrair as
        // dimensoes, e gravar em disco algo cujo tamanho so se descobre no fim deixaria arquivo
        // parcial quando o limite estoura no meio da escrita.
        using var memoria = new MemoryStream();
        await conteudo.CopyToAsync(memoria, ct);

        var bytes = memoria.ToArray();

        if (bytes.Length == 0)
            throw new InvalidOperationException("O arquivo enviado esta vazio.");

        if (bytes.LongLength > _opcoes.TamanhoMaximoBytes)
            throw new InvalidOperationException(
                $"A imagem excede o limite de {_opcoes.TamanhoMaximoBytes} bytes.");

        var agora = _relogio.UtcNow;
        var extensao = ExtensaoDe(contentType, nomeArquivo);

        // Guid puro no nome; o nome original vira apenas um prefixo legivel e ja higienizado
        // pelo SlugHelper, para o admin reconhecer o arquivo no disco.
        var prefixo = SlugHelper.Gerar(Path.GetFileNameWithoutExtension(nomeArquivo));

        if (prefixo.Length > 40)
            prefixo = prefixo[..40];

        var nomeFinal = string.IsNullOrWhiteSpace(prefixo)
            ? $"{Guid.NewGuid():N}{extensao}"
            : $"{prefixo}-{Guid.NewGuid():N}{extensao}";

        var subpasta = $"{_opcoes.Pasta.Trim('/')}/{agora:yyyy}/{agora:MM}";
        var caminhoRelativo = $"{subpasta}/{nomeFinal}";

        var pastaFisica = Path.Combine(RaizFisica(), subpasta.Replace('/', Path.DirectorySeparatorChar));
        Directory.CreateDirectory(pastaFisica);

        var caminhoFisico = Path.Combine(pastaFisica, nomeFinal);

        // FileMode.CreateNew: se por alguma razao o Guid colidir, e melhor falhar do que
        // sobrescrever a foto de outro produto em silencio.
        await using (var arquivo = new FileStream(
            caminhoFisico, FileMode.CreateNew, FileAccess.Write, FileShare.None, 81920, useAsync: true))
        {
            await arquivo.WriteAsync(bytes, ct);
        }

        var (largura, altura) = LeitorDimensoesImagem.Ler(bytes);

        _logger.LogInformation(
            "Imagem armazenada localmente. Caminho={Caminho} Bytes={Bytes}",
            caminhoRelativo,
            bytes.LongLength);

        return new ImagemArmazenadaInfo
        {
            Url = MontarUrl(caminhoRelativo),
            PublicId = caminhoRelativo,
            Largura = largura,
            Altura = altura,
            TamanhoBytes = bytes.LongLength,
            Formato = extensao.TrimStart('.')
        };
    }

    /// <inheritdoc />
    public Task RemoverAsync(string publicId, CancellationToken ct = default)
    {
        // Idempotente por contrato: a remocao roda DEPOIS do commit que apagou a linha em
        // midias. Lancar aqui deixaria a limpeza travada para sempre num arquivo fantasma.
        if (string.IsNullOrWhiteSpace(publicId))
            return Task.CompletedTask;

        // Barra no fim antes de comparar: sem ela "/app/wwwrootX/segredo" passaria pelo
        // StartsWith de "/app/wwwroot" e a protecao nao protegeria nada.
        var raiz = Path.GetFullPath(RaizFisica())
            .TrimEnd(Path.DirectorySeparatorChar) + Path.DirectorySeparatorChar;

        var caminho = Path.GetFullPath(
            Path.Combine(raiz, publicId.TrimStart('/', '\\').Replace('/', Path.DirectorySeparatorChar)));

        // Path traversal: o PublicId vem do banco, mas o banco recebeu o que alguem enviou.
        // Sem esta checagem um "../../appsettings.json" apagaria arquivo fora da pasta de midia.
        if (!caminho.StartsWith(raiz, StringComparison.Ordinal))
        {
            _logger.LogWarning("Remocao de midia recusada: caminho fora da raiz. PublicId={PublicId}", publicId);
            return Task.CompletedTask;
        }

        try
        {
            if (File.Exists(caminho))
                File.Delete(caminho);
        }
        catch (IOException excecao)
        {
            // Arquivo em uso ou disco indisponivel nao pode derrubar o fluxo de negocio que ja
            // commitou. A varredura de orfas passa de novo depois.
            _logger.LogWarning(excecao, "Nao foi possivel remover a midia local. PublicId={PublicId}", publicId);
        }
        catch (UnauthorizedAccessException excecao)
        {
            _logger.LogWarning(excecao, "Sem permissao para remover a midia local. PublicId={PublicId}", publicId);
        }

        return Task.CompletedTask;
    }

    private bool ContentTypePermitido(string contentType) =>
        _opcoes.ContentTypesPermitidos.Any(permitido =>
            string.Equals(permitido, contentType, StringComparison.OrdinalIgnoreCase));

    /// <summary>
    /// Caminho relativo e resolvido contra o diretorio de execucao, que no ASP.NET Core e o
    /// content root — a mesma pasta onde mora wwwroot, tanto em desenvolvimento quanto no
    /// container.
    /// </summary>
    private string RaizFisica()
    {
        var raiz = string.IsNullOrWhiteSpace(_opcoes.PastaRaiz) ? "wwwroot" : _opcoes.PastaRaiz;

        return Path.IsPathRooted(raiz)
            ? raiz
            : Path.Combine(Directory.GetCurrentDirectory(), raiz);
    }

    /// <summary>
    /// Base vazia devolve URL RELATIVA, que e o certo quando API e site saem pelo mesmo dominio.
    /// Com base configurada, monta absoluta para quem consome a API de outro host.
    /// </summary>
    private string MontarUrl(string caminhoRelativo)
    {
        var caminho = "/" + caminhoRelativo.TrimStart('/');

        return string.IsNullOrWhiteSpace(_opcoes.BaseUrlPublica)
            ? caminho
            : $"{_opcoes.BaseUrlPublica.TrimEnd('/')}{caminho}";
    }

    /// <summary>
    /// A extensao sai do content-type ja conferido. Confiar na extensao do nome enviado
    /// permitiria gravar ".html" servido do proprio dominio.
    /// </summary>
    private static string ExtensaoDe(string contentType, string nomeArquivo) =>
        contentType.ToLowerInvariant() switch
        {
            "image/jpeg" or "image/pjpeg" => ".jpg",
            "image/png" => ".png",
            "image/webp" => ".webp",
            "image/avif" => ".avif",
            _ => ExtensaoSegura(nomeArquivo)
        };

    private static string ExtensaoSegura(string nomeArquivo)
    {
        var extensao = Path.GetExtension(nomeArquivo);

        return string.IsNullOrWhiteSpace(extensao) || extensao.Length > 6
            ? ".bin"
            : extensao.ToLowerInvariant();
    }
}
