using Glorific.Application.DTO.Clientes;
using Glorific.Domain.Entities.Clientes;
using Mapster;

namespace Glorific.Application.Mappings;

/// <summary>
/// Lista de desejos como card de vitrine.
///
/// Tudo aqui e defensivo com null porque o repositorio le a lista com IgnoreQueryFilters: a peca
/// pode ter saido do catalogo e a variacao pode ter sido desativada. O item precisa continuar
/// aparecendo como indisponivel — sumir calado e o pior comportamento possivel justamente na
/// lista do que o cliente quer comprar quando voltar.
///
/// A expressao roda em memoria, sobre o grafo que ObterDoUsuarioAsync ja carregou.
/// </summary>
public sealed class ListaDesejoMapping : IRegister
{
    public void Register(TypeAdapterConfig config)
    {
        config.NewConfig<ListaDesejoItem, ListaDesejoItemResponseDto>()
            .MapWith(item => new ListaDesejoItemResponseDto
            {
                Id = item.Id,
                IdProduto = item.IdProduto,
                NomeProduto = item.Produto == null ? string.Empty : item.Produto.Nome,
                SlugProduto = item.Produto == null ? string.Empty : item.Produto.Slug,

                // Preco da variacao quando ela tem override; senao o preco base da peca.
                PrecoCentavos =
                    item.Variacao != null && item.Variacao.PrecoCentavos != null
                        ? item.Variacao.PrecoCentavos.Value
                        : (item.Produto == null ? 0 : item.Produto.PrecoBaseCentavos),

                PrecoComparativoCentavos = item.Produto == null
                    ? (int?)null
                    : item.Produto.PrecoComparativoCentavos,

                ImagemUrl = item.Produto == null
                    ? null
                    : item.Produto.Midias
                        .OrderBy(midia => midia.Ordem)
                        .Select(midia => midia.Midia == null ? null : midia.Midia.Url)
                        .FirstOrDefault(),

                ProdutoAtivo = item.Produto != null && item.Produto.Ativo,

                IdVariacao = item.IdVariacao,
                TamanhoVariacao = item.Variacao == null || item.Variacao.Tamanho == null
                    ? null
                    : item.Variacao.Tamanho.Codigo,
                CorVariacao = item.Variacao == null || item.Variacao.Cor == null
                    ? null
                    : item.Variacao.Cor.Nome,

                // Null quando o cliente favoritou so a peca: nao ha tamanho para responder por ele.
                VariacaoDisponivel = item.Variacao == null
                    ? (bool?)null
                    : (bool?)(item.Variacao.Ativo
                              && item.Variacao.Estoque != null
                              && item.Variacao.Estoque.Quantidade - item.Variacao.Estoque.QuantidadeReservada > 0),

                DataCriacao = item.DataCriacao
            });
    }
}
