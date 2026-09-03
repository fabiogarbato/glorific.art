using Glorific.Application.Common;
using Glorific.Application.DTO.Catalogo;
using Glorific.Application.Exceptions;
using Glorific.Application.Ports;
using Glorific.Application.Services.Interfaces;
using Glorific.Domain.Entities.Catalogo;
using Glorific.Domain.Exceptions;
using Glorific.Domain.Interfaces;
using Glorific.Domain.Interfaces.Repositories;
using MapsterMapper;

namespace Glorific.Application.Services;

/// <summary>
/// Upload de imagem e galeria do produto.
///
/// Duas regras moram aqui e nao no controller:
/// 1. O content-type e o tamanho sao conferidos ANTES de o byte chegar ao storage. Deixar isso
///    para o adaptador significaria pagar o upload inteiro para so entao recusar.
/// 2. A capa e definida por Ordem EXPLICITA, e ligar uma capa DESLIGA a anterior. Deduzir capa
///    por "menor Id" troca a foto principal a cada reupload.
/// </summary>
public class MidiaService
    : GenericService<Midia, MidiaCreateDto, MidiaUpdateDto, MidiaResponseDto>, IMidiaService
{
    /// <summary>
    /// Formatos aceitos. GIF e SVG ficam de fora de proposito: GIF nao serve para foto de
    /// produto e SVG e vetor de script — subir SVG e um XSS armazenado servido do proprio dominio.
    /// </summary>
    private static readonly HashSet<string> ContentTypesPermitidos = new(StringComparer.OrdinalIgnoreCase)
    {
        "image/jpeg",
        "image/pjpeg",
        "image/png",
        "image/webp",
        "image/avif"
    };

    /// <summary>8 MB. Foto de catalogo tratada nao passa disso; o que passa e arquivo cru de camera.</summary>
    private const long TamanhoMaximoBytes = 8L * 1024 * 1024;

    private readonly IMidiaRepository _midias;
    private readonly IProdutoRepository _produtos;
    private readonly ICorRepository _cores;
    private readonly IBaseRepository<MidiaProduto> _galeria;
    private readonly IImageStorage _storage;
    private readonly IClock _relogio;

    public MidiaService(
        IMidiaRepository midias,
        IProdutoRepository produtos,
        ICorRepository cores,
        IBaseRepository<MidiaProduto> galeria,
        IImageStorage storage,
        IUnitOfWork unitOfWork,
        IMapper mapper,
        IConsultaAssincrona consulta,
        IClock relogio)
        : base(midias, unitOfWork, mapper, consulta)
    {
        _midias = midias;
        _produtos = produtos;
        _cores = cores;
        _galeria = galeria;
        _storage = storage;
        _relogio = relogio;
    }

    protected override string NomeEntidade => "Midia";

    protected override IQueryable<Midia> AplicarOrdenacao(IQueryable<Midia> consulta) =>
        consulta.OrderByDescending(m => m.Id);

    /// <summary>Midia nao implementa IAuditable: o carimbo de criacao vem do IClock aqui.</summary>
    protected override Task AntesDeCriarAsync(Midia entidade, MidiaCreateDto dto, CancellationToken cancellationToken)
    {
        entidade.DataCriacao = _relogio.UtcNow;
        return Task.CompletedTask;
    }

    /// <inheritdoc />
    public async Task<MidiaResponseDto> EnviarAsync(
        Stream conteudo,
        string nomeArquivo,
        string contentType,
        long tamanhoBytes,
        string? altText = null,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(conteudo);

        BusinessValidationException.LancarSeVazio(nomeArquivo, "Informe o arquivo da imagem.");

        BusinessValidationException.LancarSe(
            tamanhoBytes <= 0,
            "O arquivo enviado esta vazio.");

        BusinessValidationException.LancarSe(
            tamanhoBytes > TamanhoMaximoBytes,
            $"A imagem excede o limite de {TamanhoMaximoBytes / (1024 * 1024)} MB.");

        BusinessValidationException.LancarSe(
            string.IsNullOrWhiteSpace(contentType) || !ContentTypesPermitidos.Contains(contentType),
            "Formato de imagem nao suportado. Envie JPEG, PNG, WebP ou AVIF.");

        var armazenada = await _storage.EnviarAsync(conteudo, nomeArquivo, contentType, cancellationToken);

        var midia = new Midia
        {
            Url = armazenada.Url,
            PublicId = armazenada.PublicId,
            AltText = string.IsNullOrWhiteSpace(altText) ? null : altText.Trim(),
            // Zero significa "o provedor nao informou": guardar 0 faria o front reservar um
            // espaco de altura zero e a pagina pularia quando a foto carregasse.
            Largura = armazenada.Largura > 0 ? armazenada.Largura : null,
            Altura = armazenada.Altura > 0 ? armazenada.Altura : null,
            TamanhoBytes = armazenada.TamanhoBytes ?? tamanhoBytes,
            ContentType = contentType,
            DataCriacao = _relogio.UtcNow
        };

        await _midias.AdicionarAsync(midia, cancellationToken);
        await UnitOfWork.SaveChangesAsync(cancellationToken);

        return Mapear(midia);
    }

    /// <inheritdoc />
    public async Task<IReadOnlyList<MidiaProdutoResponseDto>> ObterGaleriaAsync(
        int idProduto,
        CancellationToken cancellationToken = default)
    {
        var itens = await _midias.ObterGaleriaAsync(idProduto, cancellationToken);
        return [.. itens.Select(MapearItemGaleria)];
    }

    /// <inheritdoc />
    public async Task<MidiaProdutoResponseDto> VincularAoProdutoAsync(
        int idProduto,
        VincularMidiaProdutoDto dto,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(dto);

        if (!await _produtos.ExisteAsync(idProduto, cancellationToken))
            throw new EntityNotFoundException("Produto", idProduto);

        if (!await _midias.ExisteAsync(dto.IdMidia, cancellationToken))
            throw new EntityNotFoundException(NomeEntidade, dto.IdMidia);

        if (dto.IdCor is not null && !await _cores.ExisteAsync(dto.IdCor.Value, cancellationToken))
            throw new EntityNotFoundException("Cor", dto.IdCor.Value);

        // O indice unico (id_produto, id_midia) existe; barrar aqui evita violacao crua na tela.
        var jaVinculada = await Consulta.AlgumAsync(
            _galeria.Query().Where(mp => mp.IdProduto == idProduto && mp.IdMidia == dto.IdMidia),
            cancellationToken);

        BusinessValidationException.LancarSe(
            jaVinculada,
            "Esta imagem ja esta na galeria do produto.");

        if (dto.EhCapa)
            await DesligarCapaAtualAsync(idProduto, cancellationToken);

        var vinculo = new MidiaProduto
        {
            IdProduto = idProduto,
            IdMidia = dto.IdMidia,
            IdCor = dto.IdCor,
            Ordem = dto.Ordem,
            EhCapa = dto.EhCapa
        };

        await _galeria.AdicionarAsync(vinculo, cancellationToken);
        await UnitOfWork.SaveChangesAsync(cancellationToken);

        var galeria = await _midias.ObterGaleriaAsync(idProduto, cancellationToken);

        var criado = galeria.FirstOrDefault(mp => mp.Id == vinculo.Id)
            ?? throw new EntityNotFoundException("Item da galeria", vinculo.Id);

        return MapearItemGaleria(criado);
    }

    /// <inheritdoc />
    public async Task<IReadOnlyList<MidiaProdutoResponseDto>> ReordenarGaleriaAsync(
        int idProduto,
        ReordenarGaleriaDto dto,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(dto);

        if (!await _produtos.ExisteAsync(idProduto, cancellationToken))
            throw new EntityNotFoundException("Produto", idProduto);

        BusinessValidationException.LancarSe(
            dto.IdsNaOrdem.Count == 0,
            "Informe a nova ordem da galeria.");

        // O repositorio ignora ids que nao pertencem ao produto: o payload vem do navegador.
        await _midias.ReordenarGaleriaAsync(idProduto, dto.IdsNaOrdem, cancellationToken);
        await UnitOfWork.SaveChangesAsync(cancellationToken);

        return await ObterGaleriaAsync(idProduto, cancellationToken);
    }

    /// <inheritdoc />
    public async Task DesvincularDoProdutoAsync(
        int idProduto,
        int idMidia,
        CancellationToken cancellationToken = default)
    {
        var vinculo = await Consulta.PrimeiroOuPadraoAsync(
            _galeria.QueryTracked().Where(mp => mp.IdProduto == idProduto && mp.IdMidia == idMidia),
            cancellationToken)
            ?? throw new EntityNotFoundException("Item da galeria", idMidia);

        var eraCapa = vinculo.EhCapa;

        _galeria.Remover(vinculo);
        await UnitOfWork.SaveChangesAsync(cancellationToken);

        // O arquivo NAO e apagado do storage: a mesma midia pode estar em outro produto. A
        // varredura de orfas (IMidiaRepository.ObterOrfasAsync) e quem remove de verdade.
        if (!eraCapa)
            return;

        // Sem capa a vitrine ficaria sem foto principal. A proxima da ordem assume.
        var restantes = await _midias.ObterGaleriaAsync(idProduto, cancellationToken);

        if (restantes.Count == 0)
            return;

        await _midias.ReordenarGaleriaAsync(
            idProduto,
            [.. restantes.Select(mp => mp.Id)],
            cancellationToken);

        await UnitOfWork.SaveChangesAsync(cancellationToken);
    }

    /// <summary>Uma capa por produto. Ligar a nova sem desligar a antiga deixa duas.</summary>
    private async Task DesligarCapaAtualAsync(int idProduto, CancellationToken cancellationToken)
    {
        var capas = await Consulta.ListarAsync(
            _galeria.QueryTracked().Where(mp => mp.IdProduto == idProduto && mp.EhCapa),
            cancellationToken);

        foreach (var capa in capas)
            capa.EhCapa = false;
    }

    private static MidiaProdutoResponseDto MapearItemGaleria(MidiaProduto vinculo) =>
        new()
        {
            Id = vinculo.Id,
            IdProduto = vinculo.IdProduto,
            IdMidia = vinculo.IdMidia,
            Url = vinculo.Midia?.Url ?? string.Empty,
            AltText = vinculo.Midia?.AltText,
            Largura = vinculo.Midia?.Largura,
            Altura = vinculo.Midia?.Altura,
            IdCor = vinculo.IdCor,
            NomeCor = vinculo.Cor?.Nome,
            SlugCor = vinculo.Cor?.Slug,
            Ordem = vinculo.Ordem,
            EhCapa = vinculo.EhCapa
        };
}
