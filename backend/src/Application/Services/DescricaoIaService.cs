using Glorific.Application.Common;
using Glorific.Application.Ports;
using Glorific.Application.Services.Interfaces;
using Glorific.Domain.Interfaces.Repositories;

namespace Glorific.Application.Services;

/// <summary>
/// Orquestra a geração de texto com IA (descrição de produto e texto alternativo de imagem):
/// busca o produto/imagem, junta exemplos de outros textos já cadastrados como referência de
/// estilo, lê os bytes da foto pela porta de armazenamento e entrega tudo pronto pro adaptador
/// de IA (que não conhece banco nem disco).
///
/// Fica na Application e não na Infrastructure de propósito: "qual produto", "qual foto é a
/// capa" e "quais peças servem de referência" são regra de negócio, não detalhe de provedor.
/// </summary>
public class DescricaoIaService : IDescricaoIaService
{
    private const int MaximoExemplos = 4;

    private readonly IProdutoRepository _produtos;
    private readonly IMidiaService _midias;
    private readonly IGeradorDescricaoProduto _geradorDescricao;
    private readonly IGeradorTextoAlternativo _geradorAltText;
    private readonly IGeradorNomeProduto _geradorNome;
    private readonly IGeradorSkuProduto _geradorSku;
    private readonly ILeitorArquivoMidia _leitor;
    private readonly IConsultaAssincrona _consulta;

    public DescricaoIaService(
        IProdutoRepository produtos,
        IMidiaService midias,
        IGeradorDescricaoProduto geradorDescricao,
        IGeradorTextoAlternativo geradorAltText,
        IGeradorNomeProduto geradorNome,
        IGeradorSkuProduto geradorSku,
        ILeitorArquivoMidia leitor,
        IConsultaAssincrona consulta)
    {
        _produtos = produtos ?? throw new ArgumentNullException(nameof(produtos));
        _midias = midias ?? throw new ArgumentNullException(nameof(midias));
        _geradorDescricao = geradorDescricao ?? throw new ArgumentNullException(nameof(geradorDescricao));
        _geradorAltText = geradorAltText ?? throw new ArgumentNullException(nameof(geradorAltText));
        _geradorNome = geradorNome ?? throw new ArgumentNullException(nameof(geradorNome));
        _geradorSku = geradorSku ?? throw new ArgumentNullException(nameof(geradorSku));
        _leitor = leitor ?? throw new ArgumentNullException(nameof(leitor));
        _consulta = consulta ?? throw new ArgumentNullException(nameof(consulta));
    }

    public async Task<string> GerarSugestaoAsync(int idProduto, CancellationToken cancellationToken = default)
    {
        var consultaProduto = _produtos.Query()
            .Where(p => p.Id == idProduto)
            .Select(p => new { p.Nome, p.ComposicaoTecido });

        var produto = await _consulta.PrimeiroOuPadraoAsync(consultaProduto, cancellationToken)
            ?? throw new InvalidOperationException($"Produto {idProduto} não encontrado.");

        var galeria = await _midias.ObterGaleriaAsync(idProduto, cancellationToken);
        var capa = galeria.FirstOrDefault(m => m.EhCapa) ?? galeria.FirstOrDefault()
            ?? throw new InvalidOperationException(
                "Esta peça ainda não tem foto. Envie ao menos uma imagem antes de gerar a descrição.");

        var midiaCapa = await _midias.ObterPorIdAsync(capa.IdMidia, cancellationToken);
        var (bytes, contentType) = await LerImagemAsync(midiaCapa.PublicId, midiaCapa.ContentType, cancellationToken);

        var consultaExemplos = _produtos.Query()
            .Where(p =>
                p.Id != idProduto
                && p.Ativo
                && p.Descricao != null
                && p.Descricao != "")
            .OrderByDescending(p => p.DataCriacao)
            .Take(MaximoExemplos)
            .Select(p => p.Descricao!);

        var exemplos = await _consulta.ListarAsync(consultaExemplos, cancellationToken);

        var pedido = new DescricaoProdutoPedido
        {
            NomeProduto = produto.Nome,
            ComposicaoTecido = produto.ComposicaoTecido,
            ImagemBytes = bytes,
            ImagemContentType = contentType,
            DescricoesExemplo = exemplos,
        };

        return await _geradorDescricao.GerarAsync(pedido, cancellationToken);
    }

    public async Task<string> GerarTextoAlternativoAsync(int idMidia, CancellationToken cancellationToken = default)
    {
        var midia = await _midias.ObterPorIdAsync(idMidia, cancellationToken);
        var (bytes, contentType) = await LerImagemAsync(midia.PublicId, midia.ContentType, cancellationToken);

        var pagina = await _midias.ListarAsync(new PageRequest(1, MaximoExemplos + 1), cancellationToken);
        var exemplos = pagina.Items
            .Where(m => m.Id != idMidia && !string.IsNullOrWhiteSpace(m.AltText))
            .Select(m => m.AltText!)
            .Take(MaximoExemplos)
            .ToList();

        var pedido = new TextoAlternativoPedido
        {
            ImagemBytes = bytes,
            ImagemContentType = contentType,
            ExemplosExistentes = exemplos,
        };

        return await _geradorAltText.GerarAsync(pedido, cancellationToken);
    }

    public async Task<string> GerarNomeSugestaoAsync(int idProduto, CancellationToken cancellationToken = default)
    {
        var consultaProduto = _produtos.Query()
            .Where(p => p.Id == idProduto)
            .Select(p => new { p.Categoria.Nome });

        var produto = await _consulta.PrimeiroOuPadraoAsync(consultaProduto, cancellationToken)
            ?? throw new InvalidOperationException($"Produto {idProduto} não encontrado.");

        var galeria = await _midias.ObterGaleriaAsync(idProduto, cancellationToken);
        var capa = galeria.FirstOrDefault(m => m.EhCapa) ?? galeria.FirstOrDefault()
            ?? throw new InvalidOperationException(
                "Esta peça ainda não tem foto. Envie ao menos uma imagem antes de gerar o nome.");

        var midiaCapa = await _midias.ObterPorIdAsync(capa.IdMidia, cancellationToken);
        var (bytes, contentType) = await LerImagemAsync(midiaCapa.PublicId, midiaCapa.ContentType, cancellationToken);

        var consultaExemplos = _produtos.Query()
            .Where(p => p.Id != idProduto && p.Ativo)
            .OrderByDescending(p => p.DataCriacao)
            .Take(MaximoExemplos)
            .Select(p => p.Nome);

        var exemplos = await _consulta.ListarAsync(consultaExemplos, cancellationToken);

        var pedido = new NomeProdutoPedido
        {
            ImagemBytes = bytes,
            ImagemContentType = contentType,
            CategoriaNome = produto.Nome,
            NomesExemplo = exemplos,
        };

        return await _geradorNome.GerarAsync(pedido, cancellationToken);
    }

    public async Task<string> GerarSkuSugestaoAsync(int idProduto, CancellationToken cancellationToken = default)
    {
        var consultaProduto = _produtos.Query()
            .Where(p => p.Id == idProduto)
            .Select(p => new { p.Nome, CategoriaNome = p.Categoria.Nome });

        var produto = await _consulta.PrimeiroOuPadraoAsync(consultaProduto, cancellationToken)
            ?? throw new InvalidOperationException($"Produto {idProduto} não encontrado.");

        var consultaExemplos = _produtos.Query()
            .Where(p => p.Id != idProduto && p.Ativo)
            .OrderByDescending(p => p.DataCriacao)
            .Take(MaximoExemplos)
            .Select(p => p.Nome + " → " + p.SkuBase);

        var exemplos = await _consulta.ListarAsync(consultaExemplos, cancellationToken);

        var pedido = new SkuProdutoPedido
        {
            NomeProduto = produto.Nome,
            CategoriaNome = produto.CategoriaNome,
            ExemplosSku = exemplos,
        };

        return await _geradorSku.GerarAsync(pedido, cancellationToken);
    }

    /// <summary>
    /// Mesma resolução de caminho físico do ArmazenamentoLocalImagem (raiz + PublicId relativo),
    /// pela porta ILeitorArquivoMidia — a Application não sabe onde/como o arquivo é guardado.
    /// </summary>
    private async Task<(byte[] Bytes, string ContentType)> LerImagemAsync(
        string? publicId,
        string? contentType,
        CancellationToken ct)
    {
        if (string.IsNullOrWhiteSpace(publicId))
            throw new InvalidOperationException("A imagem não tem um arquivo associado no acervo.");

        var bytes = await _leitor.LerBytesAsync(publicId, ct);
        return (bytes, string.IsNullOrWhiteSpace(contentType) ? "image/jpeg" : contentType);
    }
}
