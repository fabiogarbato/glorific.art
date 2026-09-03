using Glorific.Application.DTO.Pedidos;
using Glorific.Domain.Entities.Pedidos;
using Mapster;

namespace Glorific.Application.Mappings;

/// <summary>
/// Mapeamentos da area de pedidos.
///
/// Aqui moram SO as projecoes rasas e nome-a-nome. O detalhe do pedido (PedidoResponseDto) e
/// montado a mao no PedidoService de proposito: ele agrega quatro agregados diferentes (itens,
/// endereco owned, pagamento, envio, historico) e uma regra de visibilidade — a URL da etiqueta
/// nao pode vazar para o cliente final. Regra de visibilidade dentro de mapeamento e o tipo de
/// coisa que ninguem revisa e vira vazamento silencioso.
///
/// Todo campo cujo nome muda entre entidade e DTO esta declarado explicitamente: os campos do
/// item terminam em "Snapshot" na entidade justamente para lembrar quem le que aquilo e valor
/// congelado, e o DTO nao carrega esse sufixo.
/// </summary>
public sealed class PedidoMapping : IRegister
{
    public void Register(TypeAdapterConfig config)
    {
        config.NewConfig<PedidoItem, PedidoItemResponseDto>()
            .Map(destino => destino.Sku, origem => origem.SkuSnapshot)
            .Map(destino => destino.NomeProduto, origem => origem.NomeProdutoSnapshot)
            .Map(destino => destino.Tamanho, origem => origem.TamanhoSnapshot)
            .Map(destino => destino.Cor, origem => origem.CorSnapshot)
            .Map(destino => destino.ImagemUrl, origem => origem.ImagemUrlSnapshot);

        // Owned type: nome a nome, sem nenhuma derivacao.
        config.NewConfig<PedidoEnderecoSnapshot, PedidoEnderecoResponseDto>();

        config.NewConfig<PedidoHistorico, PedidoHistoricoResponseDto>()
            // Enum anulavel para string anulavel: a conversao implicita do Mapster resolveria,
            // mas escrever aqui e o que garante que "null" continue sendo null e nao "0".
            .Map(destino => destino.StatusAnterior,
                origem => origem.StatusAnterior == null ? (string?)null : origem.StatusAnterior.Value.ToString())
            .Map(destino => destino.StatusNovo, origem => origem.StatusNovo.ToString());

        config.NewConfig<EnvioEvento, RastreioEventoResponseDto>()
            .Map(destino => destino.Status, origem => origem.Status.ToString());
    }
}
