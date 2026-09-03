using Glorific.Application.DTO.Catalogo;
using Glorific.Domain.Entities.Catalogo;
using Mapster;

namespace Glorific.Application.Mappings;

/// <summary>
/// Mapeamentos do catalogo.
///
/// Tres decisoes valem o comentario:
///
/// 1. CreateDto -&gt; entidade usa MapWith com o construtor escrito a mao. As entidades do
///    catalogo tem membros "required" (Nome, Slug, SkuBase, HexRgb, Sku): deixar o Mapster
///    montar por convencao funciona, mas qualquer renomeacao futura vira campo silenciosamente
///    nulo em producao. Escrito assim, renomear quebra a COMPILACAO.
///
/// 2. O Slug nasce vazio no mapeamento e e preenchido pelo servico (GeradorSlug), que precisa
///    consultar o banco para desambiguar. Mapeamento e sincrono; unicidade nao.
///
/// 3. Produto e ProdutoVariacao NAO tem mapeamento entidade -&gt; Response aqui de proposito:
///    a resposta deles depende de tamanho, cor, estoque e galeria, e e montada explicitamente
///    no servico (override de Mapear). Convencao de nome nao alcanca "quantidade disponivel".
/// </summary>
public sealed class CatalogoMapping : IRegister
{
    public void Register(TypeAdapterConfig config)
    {
        RegistrarCategoria(config);
        RegistrarColecao(config);
        RegistrarTamanho(config);
        RegistrarCor(config);
        RegistrarMidia(config);
        RegistrarTabelaMedidas(config);
        RegistrarProduto(config);
    }

    private static void RegistrarProduto(TypeAdapterConfig config)
    {
        // Ativo = true no nascimento: produto novo entra publicavel, e a despublicacao e sempre
        // um ato explicito que gera LogProduto.
        config.NewConfig<ProdutoCreateDto, Produto>()
            .MapWith(dto => new Produto
            {
                Nome = dto.Nome,
                Slug = string.Empty,
                SkuBase = dto.SkuBase.Trim().ToUpperInvariant(),
                Descricao = dto.Descricao,
                IdCategoria = dto.IdCategoria,
                Genero = dto.Genero,
                PrecoBaseCentavos = dto.PrecoBaseCentavos,
                PrecoComparativoCentavos = dto.PrecoComparativoCentavos,
                ComposicaoTecido = dto.ComposicaoTecido,
                InstrucoesLavagem = dto.InstrucoesLavagem,
                Modelagem = dto.Modelagem,
                IdTabelaMedidas = dto.IdTabelaMedidas,
                Destaque = dto.Destaque,
                MetaTitle = dto.MetaTitle,
                MetaDescription = dto.MetaDescription,
                Ativo = true
            });

        // O que fica de FORA da edicao e a parte importante:
        // - Slug: trocar endereco publicado quebra link indexado, so muda por pedido explicito.
        // - Ativo: publicar e despublicar tem caminho proprio, com LogProduto.
        // - NotaMedia/TotalAvaliacoes: sao denormalizacoes recalculadas pela moderacao de
        //   avaliacao. Deixar o corpo do PUT escrever nelas seria abrir a nota da vitrine para
        //   qualquer chamada de edicao de catalogo.
        config.NewConfig<ProdutoUpdateDto, Produto>()
            .Map(destino => destino.SkuBase, origem => origem.SkuBase.Trim().ToUpperInvariant())
            .Ignore(destino => destino.Id)
            .Ignore(destino => destino.Slug)
            .Ignore(destino => destino.Ativo)
            .Ignore(destino => destino.NotaMedia!)
            .Ignore(destino => destino.TotalAvaliacoes)
            .Ignore(destino => destino.DataCriacao)
            .Ignore(destino => destino.DataAlteracao!);
    }

    private static void RegistrarCategoria(TypeAdapterConfig config)
    {
        config.NewConfig<CategoriaCreateDto, Categoria>()
            .MapWith(dto => new Categoria
            {
                Nome = dto.Nome,
                Slug = string.Empty,
                Descricao = dto.Descricao,
                IdCategoriaPai = dto.IdCategoriaPai,
                IdMidiaCapa = dto.IdMidiaCapa,
                Ordem = dto.Ordem,
                Habilitado = dto.Habilitado,
                MetaTitle = dto.MetaTitle,
                MetaDescription = dto.MetaDescription
            });

        // Update mapeia SOBRE a instancia carregada. Slug fica de fora: quem decide troca de
        // slug e o servico, porque mudar endereco publicado quebra link ja indexado.
        config.NewConfig<CategoriaUpdateDto, Categoria>()
            .Ignore(destino => destino.Slug)
            .Ignore(destino => destino.Id)
            .Ignore(destino => destino.DataCriacao)
            .Ignore(destino => destino.DataAlteracao!);

        config.NewConfig<Categoria, CategoriaResponseDto>()
            .Map(destino => destino.UrlMidiaCapa, origem => origem.MidiaCapa == null ? null : origem.MidiaCapa.Url)
            // Teto de profundidade proprio: categorias sao auto-relacionadas e a arvore do
            // projeto tem UM nivel. Sem o teto, uma configuracao errada de pai vira recursao.
            .MaxDepth(2);
    }

    private static void RegistrarColecao(TypeAdapterConfig config)
    {
        config.NewConfig<ColecaoCreateDto, Colecao>()
            .MapWith(dto => new Colecao
            {
                Nome = dto.Nome,
                Slug = string.Empty,
                Descricao = dto.Descricao,
                Epigrafe = dto.Epigrafe,
                IdMidiaCapa = dto.IdMidiaCapa,
                IdMidiaBanner = dto.IdMidiaBanner,
                DataInicio = dto.DataInicio,
                DataFim = dto.DataFim,
                Destaque = dto.Destaque,
                Habilitado = dto.Habilitado,
                Ordem = dto.Ordem
            });

        config.NewConfig<ColecaoUpdateDto, Colecao>()
            .Ignore(destino => destino.Slug)
            .Ignore(destino => destino.Id)
            .Ignore(destino => destino.DataCriacao)
            .Ignore(destino => destino.DataAlteracao!);

        config.NewConfig<Colecao, ColecaoResponseDto>()
            .Map(destino => destino.UrlMidiaCapa, origem => origem.MidiaCapa == null ? null : origem.MidiaCapa.Url)
            .Map(destino => destino.UrlMidiaBanner, origem => origem.MidiaBanner == null ? null : origem.MidiaBanner.Url);
    }

    private static void RegistrarTamanho(TypeAdapterConfig config)
    {
        // Codigo normalizado no proprio mapeamento: "p " e "P" sao o mesmo tamanho, e o indice
        // unico (grade, codigo) nao sabe disso. Normalizar so no create deixaria a edicao
        // reintroduzir a duplicata que a criacao barrou.
        config.NewConfig<TamanhoCreateDto, Tamanho>()
            .MapWith(dto => new Tamanho
            {
                Codigo = dto.Codigo.Trim().ToUpperInvariant(),
                Descricao = dto.Descricao,
                Ordem = dto.Ordem,
                Grade = dto.Grade,
                Ativo = dto.Ativo
            });

        config.NewConfig<TamanhoUpdateDto, Tamanho>()
            .Map(destino => destino.Codigo, origem => origem.Codigo.Trim().ToUpperInvariant())
            .Ignore(destino => destino.Id);

        config.NewConfig<Tamanho, TamanhoResponseDto>();

        config.NewConfig<Tamanho, TamanhoVitrineDto>();
    }

    private static void RegistrarCor(TypeAdapterConfig config)
    {
        config.NewConfig<CorCreateDto, Cor>()
            .MapWith(dto => new Cor
            {
                Nome = dto.Nome,
                Slug = string.Empty,
                HexRgb = dto.HexRgb.Trim().ToLowerInvariant(),
                IdMidiaSwatch = dto.IdMidiaSwatch,
                Ordem = dto.Ordem,
                Ativo = dto.Ativo
            });

        // Hex normalizado tambem na edicao: "#FFF0E1" e "#fff0e1" pintam a mesma bolinha, e
        // gravar as duas formas faz o front comparar cor por string e errar.
        config.NewConfig<CorUpdateDto, Cor>()
            .Map(destino => destino.HexRgb, origem => origem.HexRgb.Trim().ToLowerInvariant())
            .Ignore(destino => destino.Slug)
            .Ignore(destino => destino.Id);

        config.NewConfig<Cor, CorResponseDto>()
            .Map(destino => destino.UrlMidiaSwatch, origem => origem.MidiaSwatch == null ? null : origem.MidiaSwatch.Url);

        config.NewConfig<Cor, CorVitrineDto>()
            .Map(destino => destino.UrlSwatch, origem => origem.MidiaSwatch == null ? null : origem.MidiaSwatch.Url);
    }

    private static void RegistrarMidia(TypeAdapterConfig config)
    {
        config.NewConfig<MidiaCreateDto, Midia>()
            .MapWith(dto => new Midia
            {
                Url = dto.Url,
                PublicId = dto.PublicId,
                AltText = dto.AltText,
                Largura = dto.Largura,
                Altura = dto.Altura,
                TamanhoBytes = dto.TamanhoBytes,
                ContentType = dto.ContentType
            });

        // Somente AltText. Trocar a Url de uma midia ja vinculada mudaria a foto de todo produto
        // que a referencia, sem deixar rastro.
        config.NewConfig<MidiaUpdateDto, Midia>()
            .Ignore(destino => destino.Id)
            .Ignore(destino => destino.Url)
            .Ignore(destino => destino.PublicId!)
            .Ignore(destino => destino.Largura!)
            .Ignore(destino => destino.Altura!)
            .Ignore(destino => destino.TamanhoBytes!)
            .Ignore(destino => destino.ContentType!)
            .Ignore(destino => destino.DataCriacao);

        config.NewConfig<Midia, MidiaResponseDto>();
    }

    private static void RegistrarTabelaMedidas(TypeAdapterConfig config)
    {
        // As linhas nao entram no mapeamento: elas sao substituidas em bloco pelo servico,
        // que precisa remover as antigas para nao deixar linha orfa de tamanho retirado da grade.
        config.NewConfig<TabelaMedidasCreateDto, TabelaMedidas>()
            .MapWith(dto => new TabelaMedidas
            {
                Nome = dto.Nome,
                Observacao = dto.Observacao,
                Ativo = dto.Ativo
            });

        config.NewConfig<TabelaMedidasUpdateDto, TabelaMedidas>()
            .Ignore(destino => destino.Id)
            .Ignore(destino => destino.Linhas)
            .Ignore(destino => destino.DataCriacao);

        config.NewConfig<TabelaMedidasLinha, TabelaMedidasLinhaResponseDto>()
            .Map(destino => destino.CodigoTamanho, origem => origem.Tamanho == null ? string.Empty : origem.Tamanho.Codigo);

        config.NewConfig<TabelaMedidas, TabelaMedidasResponseDto>();
    }
}
