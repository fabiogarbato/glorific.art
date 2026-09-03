using Glorific.Domain.Common;

namespace Glorific.Domain.Entities.Config;

/// <summary>
/// Linha unica de configuracao operacional da loja, com colunas TIPADAS em vez de chave/valor
/// em texto. Chave/valor generico obriga parse e validacao espalhados por cada leitor, e o erro
/// de digitacao do admin ("dez" em PrazoManuseioDias) so aparece na hora de cotar frete.
/// Cacheada em memoria: e lida em toda cotacao e em toda pagina de produto.
/// </summary>
public class ConfiguracaoLoja : BaseEntity, IAuditable
{
    /// <summary>Acima deste valor em centavos o frete sai zerado. Null desliga a regra.</summary>
    public int? FreteGratisAcimaDeCentavos { get; set; }

    /// <summary>Dias uteis entre o pagamento e a postagem. Entra no prazo exibido ao cliente.</summary>
    public int PrazoManuseioDias { get; set; } = 2;

    /// <summary>CEP de origem das cotacoes, so digitos.</summary>
    public required string CepOrigem { get; set; }

    public int PoliticaTrocaDias { get; set; } = 7;

    public int? PedidoMinimoCentavos { get; set; }

    /// <summary>Exibir "ultimas pecas" na vitrine cria urgencia, mas tambem expoe o estoque ao concorrente.</summary>
    public bool ExibirEstoqueBaixo { get; set; }

    public int LimiteEstoqueBaixo { get; set; } = 3;

    public DateTime DataCriacao { get; set; }
    public DateTime? DataAlteracao { get; set; }
}
