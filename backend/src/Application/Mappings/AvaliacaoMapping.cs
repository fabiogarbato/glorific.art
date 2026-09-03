using Glorific.Application.DTO.Social;
using Glorific.Domain.Entities.Social;
using Mapster;

namespace Glorific.Application.Mappings;

/// <summary>
/// Mapeamento da avaliacao para a vitrine.
///
/// Usa MapWith e nao a convencao de nomes de proposito: o DTO publico e uma PROJECAO com regra
/// (nome abreviado, selo de compra verificada derivado de IdPedidoItem, midias achatadas em URL),
/// e convencao de nome nao expressa nada disso. Escrever o objeto inteiro tambem torna impossivel
/// vazar um campo novo da entidade para a vitrine sem alguem digitar a linha.
///
/// A expressao roda EM MEMORIA, depois de o repositorio ter materializado a avaliacao com Usuario
/// e Midias carregados por QueryAprovadasDoProduto. Ela nao e traduzida para SQL.
/// </summary>
public sealed class AvaliacaoMapping : IRegister
{
    public void Register(TypeAdapterConfig config)
    {
        config.NewConfig<AvaliacaoMidia, AvaliacaoMidiaResponseDto>()
            .MapWith(midia => new AvaliacaoMidiaResponseDto
            {
                Id = midia.Id,
                Url = midia.Midia.Url,
                AltText = midia.Midia.AltText,
                Ordem = midia.Ordem
            });

        config.NewConfig<Avaliacao, AvaliacaoResponseDto>()
            .MapWith(avaliacao => new AvaliacaoResponseDto
            {
                Id = avaliacao.Id,
                IdProduto = avaliacao.IdProduto,
                Nota = avaliacao.Nota,
                Titulo = avaliacao.Titulo,
                Comentario = avaliacao.Comentario,
                Autor = ApresentacaoAutor.Abreviar(
                    avaliacao.Usuario == null ? null : avaliacao.Usuario.NomeCompleto),
                CompraVerificada = avaliacao.IdPedidoItem != null,
                TamanhoComprado = avaliacao.TamanhoComprado,
                AlturaClienteCm = avaliacao.AlturaClienteCm,
                PesoClienteKg = avaliacao.PesoClienteKg,
                Caimento = avaliacao.Caimento,
                Recomenda = avaliacao.Recomenda,
                Status = avaliacao.Status,
                DataCriacao = avaliacao.DataCriacao,
                Midias = avaliacao.Midias
                    .OrderBy(midia => midia.Ordem)
                    .Select(midia => new AvaliacaoMidiaResponseDto
                    {
                        Id = midia.Id,
                        Url = midia.Midia.Url,
                        AltText = midia.Midia.AltText,
                        Ordem = midia.Ordem
                    })
                    .ToList()
            });
    }
}
