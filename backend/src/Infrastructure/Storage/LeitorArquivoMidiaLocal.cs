using Glorific.Application.Ports;
using Microsoft.Extensions.Options;

namespace Glorific.Infrastructure.Storage;

/// <summary>
/// Lê bytes de uma mídia gravada pelo ArmazenamentoLocalImagem. Mesma resolução de caminho
/// físico (raiz + PublicId relativo, com a mesma checagem de path traversal), só que pra LER em
/// vez de escrever/remover — por isso não reaproveita aquela classe.
/// </summary>
public sealed class LeitorArquivoMidiaLocal : ILeitorArquivoMidia
{
    private readonly ArmazenamentoLocalOptions _opcoes;

    public LeitorArquivoMidiaLocal(IOptions<ArmazenamentoLocalOptions> opcoes)
    {
        _opcoes = opcoes?.Value ?? new ArmazenamentoLocalOptions();
    }

    public Task<byte[]> LerBytesAsync(string publicId, CancellationToken ct = default)
    {
        if (string.IsNullOrWhiteSpace(publicId))
            throw new InvalidOperationException("PublicId da mídia não informado.");

        var raiz = string.IsNullOrWhiteSpace(_opcoes.PastaRaiz) ? "wwwroot" : _opcoes.PastaRaiz;
        var raizFisica = Path.IsPathRooted(raiz) ? raiz : Path.Combine(Directory.GetCurrentDirectory(), raiz);

        var raizNormalizada = Path.GetFullPath(raizFisica).TrimEnd(Path.DirectorySeparatorChar)
            + Path.DirectorySeparatorChar;

        var caminho = Path.GetFullPath(
            Path.Combine(raizFisica, publicId.TrimStart('/', '\\').Replace('/', Path.DirectorySeparatorChar)));

        // Path traversal: mesma protecao do ArmazenamentoLocalImagem.RemoverAsync — um PublicId
        // manipulado nao pode ler arquivo fora da pasta de midia.
        if (!caminho.StartsWith(raizNormalizada, StringComparison.Ordinal))
            throw new InvalidOperationException("Caminho da mídia fora da raiz de armazenamento.");

        if (!File.Exists(caminho))
            throw new InvalidOperationException("Arquivo da mídia não encontrado no armazenamento.");

        return File.ReadAllBytesAsync(caminho, ct);
    }
}
