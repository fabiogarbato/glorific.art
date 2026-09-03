namespace Glorific.Domain.Enums;

public enum StatusPedido
{
    AguardandoPagamento = 1,
    Pago = 2,
    EmSeparacao = 3,
    Enviado = 4,
    Entregue = 5,
    Cancelado = 6,
    PagamentoRecusado = 7,
    EmDevolucao = 8,
    Devolvido = 9,
    Estornado = 10
}

public enum StatusPagamento
{
    Pendente = 1,
    Aprovado = 2,
    Recusado = 3,
    Expirado = 4,
    Cancelado = 5,
    Estornado = 6
}

public enum StatusEnvio
{
    Pendente = 1,
    NoCarrinho = 2,
    Comprado = 3,
    EtiquetaGerada = 4,
    Postado = 5,
    Entregue = 6,
    Cancelado = 7,
    Falha = 8,
    AguardandoNota = 9
}

public enum StatusAvaliacao
{
    Pendente = 1,
    Aprovada = 2,
    Rejeitada = 3
}

public enum StatusCarrinho
{
    Aberto = 1,
    Convertido = 2,
    Abandonado = 3,
    Expirado = 4
}

public enum TipoCupom
{
    Percentual = 1,
    ValorFixo = 2,
    FreteGratis = 3
}

public enum GeneroProduto
{
    Feminino = 1,
    Masculino = 2,
    Unissex = 3,
    Infantil = 4
}

/// <summary>Separa a numeracao de calca (36-46) da grade alfabetica (PP-XG).</summary>
public enum GradeTamanho
{
    Alfa = 1,
    Numerica = 2,
    Unico = 3,
    Infantil = 4
}

public enum ModelagemProduto
{
    Justa = 1,
    Reta = 2,
    Ampla = 3,
    Oversized = 4
}

/// <summary>Feedback de caimento na avaliacao — o dado que mais reduz devolucao em moda.</summary>
public enum CaimentoTamanho
{
    MuitoPequeno = 1,
    Pequeno = 2,
    Certo = 3,
    Grande = 4,
    MuitoGrande = 5
}
