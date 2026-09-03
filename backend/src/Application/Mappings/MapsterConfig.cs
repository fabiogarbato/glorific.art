using System.Reflection;
using Mapster;

namespace Glorific.Application.Mappings;

/// <summary>
/// Configuracao unica do Mapster.
///
/// O que se corrige aqui: no repo de referencia o AddMapster() NAO fazia scan e apenas UM
/// mapeamento estava de fato ativo em runtime — os outros dezesseis valiam "por convencao de
/// nome", com um comentario admitindo o buraco. O sintoma so aparecia em producao, num campo
/// que vinha nulo.
///
/// Aqui o Scan e de verdade e o Compile e obrigatorio: se algum IRegister tiver mapeamento
/// quebrado (propriedade inexistente, tipo incompativel), a API NAO SOBE. Falhar no boot e
/// barato; falhar no checkout do cliente nao e.
/// </summary>
public static class MapsterConfig
{
    /// <summary>
    /// Varre os assemblies em busca de IRegister, aplica e compila tudo.
    /// Chamado por AddApplication no boot — nenhum outro lugar deve mexer no GlobalSettings.
    /// </summary>
    public static TypeAdapterConfig Registrar(params Assembly[] assemblies)
    {
        var config = TypeAdapterConfig.GlobalSettings;

        var alvos = assemblies is { Length: > 0 }
            ? assemblies
            : [typeof(MapsterConfig).Assembly];

        // Entidade tem navegacao circular (Produto -> Variacoes -> Produto). Sem o teto de
        // profundidade, um mapeamento distraido para um DTO que exponha a volta entra em
        // recursao e derruba a requisicao com StackOverflow, que nem excecao gera.
        config.Default.MaxDepth(3);

        config.Scan(alvos);

        // Compila TUDO agora. Este e o ponto em que um mapeamento quebrado derruba o boot.
        config.Compile();

        return config;
    }
}
