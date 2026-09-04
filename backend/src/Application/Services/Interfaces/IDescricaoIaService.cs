namespace Glorific.Application.Services.Interfaces;

public interface IDescricaoIaService
{
    /// <summary>
    /// Gera uma sugestão de descrição pra um produto já cadastrado, a partir da foto de capa da
    /// galeria e de descrições de outras peças ativas como referência de estilo. Não salva nada
    /// — devolve o texto pro admin revisar e só então gravar pelo PUT normal do produto.
    /// </summary>
    Task<string> GerarSugestaoAsync(int idProduto, CancellationToken cancellationToken = default);

    /// <summary>
    /// Gera uma sugestão de TEXTO ALTERNATIVO pra uma imagem já enviada ao acervo, a partir da
    /// própria foto e de alt texts de outras imagens como referência de padrão. Não salva nada.
    /// </summary>
    Task<string> GerarTextoAlternativoAsync(int idMidia, CancellationToken cancellationToken = default);

    /// <summary>
    /// Gera uma sugestão de NOME pra um produto já cadastrado, a partir da foto de capa e de
    /// nomes de outras peças como referência de formato. Não salva nada.
    /// </summary>
    Task<string> GerarNomeSugestaoAsync(int idProduto, CancellationToken cancellationToken = default);

    /// <summary>
    /// Gera uma sugestão de SKU BASE pra um produto já cadastrado, seguindo o padrão de código
    /// já usado em outras peças. Não salva nada.
    /// </summary>
    Task<string> GerarSkuSugestaoAsync(int idProduto, CancellationToken cancellationToken = default);
}
