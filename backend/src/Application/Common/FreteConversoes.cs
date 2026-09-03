using System.Globalization;

namespace Glorific.Application.Common;

/// <summary>
/// Conversoes de unidade da fronteira de frete.
///
/// Dentro do sistema dinheiro e SEMPRE int em centavos e peso e SEMPRE int em gramas. O Melhor
/// Envio fala reais decimais e quilos decimais. Concentrar as duas conversoes aqui evita o que
/// deu errado no repo de referencia: cada chamada dividindo por 100 e por 1000 na mao, com
/// arredondamento diferente em cada lugar e um caso de peso enviado em gramas (cotacao de
/// 400 kg para um vestido).
/// </summary>
public static class FreteConversoes
{
    /// <summary>
    /// Gramas para quilos com 3 casas — a precisao que o Melhor Envio aceita (1 g).
    /// Piso de 0,001 kg: peso zero faz o ME recusar a cotacao inteira com 422.
    /// </summary>
    public static decimal GramasParaKg(int gramas)
    {
        if (gramas <= 0)
            return 0.001m;

        return Math.Round(gramas / 1000m, 3, MidpointRounding.AwayFromZero);
    }

    /// <summary>Centavos para reais decimais (o que vai no insuranceValue da cotacao).</summary>
    public static decimal CentavosParaReais(int centavos) =>
        Math.Round(centavos / 100m, 2, MidpointRounding.AwayFromZero);

    /// <summary>
    /// Centavos para a STRING que o contrato do ME exige em products[].unitary_value e
    /// products[].quantity do POST /api/cart. Cultura invariante de proposito: em pt-BR o
    /// separador decimal e virgula e "189,90" e recusado pelo parser do parceiro.
    /// </summary>
    public static string CentavosParaTexto(int centavos) =>
        CentavosParaReais(centavos).ToString("0.00", CultureInfo.InvariantCulture);

    /// <summary>Reais decimais vindos do ME para centavos inteiros.</summary>
    public static int ReaisParaCentavos(decimal reais) =>
        (int)Math.Round(reais * 100m, MidpointRounding.AwayFromZero);

    /// <summary>Versao tolerante a null, para campo opcional da resposta do parceiro.</summary>
    public static int? ReaisParaCentavos(decimal? reais) =>
        reais is null ? null : ReaisParaCentavos(reais.Value);
}
