using Glorific.Domain.Entities.Catalogo;
using Glorific.Domain.Entities.Estoque;
using Glorific.Domain.Entities.Identidade;
using Glorific.Domain.Entities.Pedidos;
using Glorific.Domain.Entities.Promocoes;
using Glorific.Domain.Enums;
using Glorific.Infrastructure.Data;
using Microsoft.EntityFrameworkCore;

namespace Glorific.Tests.Persistencia;

/// <summary>
/// Ids do cenario minimo de catalogo com estoque. Record para o teste poder desestruturar
/// sem inventar tuplas anonimas em cada arquivo.
/// </summary>
internal sealed record CenarioCatalogo(int IdCategoria, int IdProduto, int IdVariacao, int IdEstoque);

/// <summary>
/// Fabrica de dado de teste gravado no Postgres REAL.
///
/// Regra que vale para tudo aqui: nada de HasData, nada de dado escondido no fixture. Cada teste
/// monta explicitamente o que precisa, com sufixo proprio nas chaves unicas (slug, sku, e-mail),
/// para que dois cenarios no mesmo teste nao colidam nos indices UNIQUE do banco.
///
/// Tamanho e Cor vem do SeedInicial — sao dado de REFERENCIA, e recria-los a cada cenario
/// esconderia justamente o caso em que o seed nao rodou.
/// </summary>
internal static class DadosPersistencia
{
    /// <summary>
    /// Categoria + Produto + Variacao + linha de estoque, tudo ativo e coerente com os CHECK
    /// do banco (peso e dimensoes positivos, reservada &lt;= quantidade).
    /// </summary>
    public static async Task<CenarioCatalogo> CriarCatalogoComEstoqueAsync(
        GlorificContext contexto,
        int quantidade,
        int reservada = 0,
        string sufixo = "a",
        CancellationToken cancellationToken = default)
    {
        var idTamanho = await contexto.Tamanhos
            .AsNoTracking()
            .OrderBy(t => t.Id)
            .Select(t => t.Id)
            .FirstAsync(cancellationToken);

        var idCor = await contexto.Cores
            .AsNoTracking()
            .OrderBy(c => c.Id)
            .Select(c => c.Id)
            .FirstAsync(cancellationToken);

        var categoria = new Categoria
        {
            Nome = $"Categoria {sufixo}",
            Slug = $"categoria-{sufixo}",
            Ordem = 10
        };

        await contexto.Categorias.AddAsync(categoria, cancellationToken);
        await contexto.SaveChangesAsync(cancellationToken);

        var produto = new Produto
        {
            Nome = $"Vestido {sufixo}",
            Slug = $"vestido-{sufixo}",
            SkuBase = $"VST-{sufixo.ToUpperInvariant()}",
            IdCategoria = categoria.Id,
            Genero = GeneroProduto.Feminino,
            PrecoBaseCentavos = 24900,
            Ativo = true
        };

        await contexto.Produtos.AddAsync(produto, cancellationToken);
        await contexto.SaveChangesAsync(cancellationToken);

        var variacao = new ProdutoVariacao
        {
            IdProduto = produto.Id,
            Sku = $"VST-{sufixo.ToUpperInvariant()}-01",
            IdTamanho = idTamanho,
            IdCor = idCor,
            PrecoCentavos = 24900,
            // Positivos por exigencia do ck_produto_variacoes_dimensoes: sem eles o Melhor Envio
            // devolve 422 na cotacao, e o CHECK existe para impedir a linha nascer assim.
            PesoGramas = 420,
            AlturaCm = 5m,
            LarguraCm = 30m,
            ComprimentoCm = 40m,
            Ativo = true
        };

        await contexto.ProdutoVariacoes.AddAsync(variacao, cancellationToken);
        await contexto.SaveChangesAsync(cancellationToken);

        var estoque = new EstoqueVariacao
        {
            IdVariacao = variacao.Id,
            Quantidade = quantidade,
            QuantidadeReservada = reservada,
            QuantidadeMinima = 0
        };

        await contexto.EstoquesVariacoes.AddAsync(estoque, cancellationToken);
        await contexto.SaveChangesAsync(cancellationToken);

        return new CenarioCatalogo(categoria.Id, produto.Id, variacao.Id, estoque.Id);
    }

    /// <summary>Estoque recem-lido do banco, sem tracking. Toda assercao de saldo passa por aqui.</summary>
    public static Task<EstoqueVariacao> LerEstoqueAsync(
        GlorificContext contexto,
        int idVariacao,
        CancellationToken cancellationToken = default) =>
        contexto.EstoquesVariacoes
            .AsNoTracking()
            .FirstAsync(e => e.IdVariacao == idVariacao, cancellationToken);

    public static async Task<Usuario> CriarUsuarioAsync(
        GlorificContext contexto,
        string sufixo = "a",
        CancellationToken cancellationToken = default)
    {
        var usuario = new Usuario
        {
            Uuid = Guid.NewGuid().ToString(),
            Email = $"cliente-{sufixo}@glorific.test",
            NomeCompleto = $"Cliente {sufixo}",
            EmailVerificado = true,
            Ativo = true
        };

        await contexto.Usuarios.AddAsync(usuario, cancellationToken);
        await contexto.SaveChangesAsync(cancellationToken);

        return usuario;
    }

    /// <summary>
    /// Cupom com codigo ja normalizado em maiusculas, como o repositorio espera encontrar.
    /// </summary>
    public static async Task<Cupom> CriarCupomAsync(
        GlorificContext contexto,
        string codigo,
        int? usoMaximoTotal,
        bool ativo = true,
        CancellationToken cancellationToken = default)
    {
        var cupom = new Cupom
        {
            Codigo = codigo.ToUpperInvariant(),
            Descricao = "Cupom de teste",
            Tipo = TipoCupom.Percentual,
            Valor = 1000,
            UsoMaximoTotal = usoMaximoTotal,
            UsoMaximoPorUsuario = 99,
            UsosAtuais = 0,
            VigenciaInicio = new DateTime(2026, 1, 1, 0, 0, 0, DateTimeKind.Utc),
            VigenciaFim = null,
            Ativo = ativo
        };

        await contexto.Cupons.AddAsync(cupom, cancellationToken);
        await contexto.SaveChangesAsync(cancellationToken);

        return cupom;
    }

    /// <summary>
    /// Pedido com uma linha, cobrindo o que o historico precisa: as FKs de relatorio
    /// (id_produto, id_variacao) E o snapshot autossuficiente que o recibo exibe.
    /// </summary>
    public static async Task<Pedido> CriarPedidoComItemAsync(
        GlorificContext contexto,
        int idUsuario,
        CenarioCatalogo catalogo,
        string numero,
        CancellationToken cancellationToken = default)
    {
        var pedido = new Pedido
        {
            Numero = numero,
            Uuid = Guid.NewGuid().ToString(),
            IdUsuario = idUsuario,
            Status = StatusPedido.Pago,
            SubtotalCentavos = 24900,
            DescontoCupomCentavos = 0,
            FreteCentavos = 2500,
            TotalCentavos = 27400,
            PesoTotalGramas = 420,
            // Pedido nao e IAuditable: a data e do caso de uso, nao da auditoria do DbContext.
            DataCriacao = new DateTime(2026, 2, 1, 10, 0, 0, DateTimeKind.Utc),
            DataPagamento = new DateTime(2026, 2, 1, 10, 5, 0, DateTimeKind.Utc),
            EnderecoEntrega = new PedidoEnderecoSnapshot
            {
                Destinatario = "Maria Souza",
                DocumentoDestinatario = "39053344705",
                TelefoneContato = "41999990000",
                Cep = "80010000",
                Logradouro = "Rua XV de Novembro",
                Numero = "100",
                Bairro = "Centro",
                Cidade = "Curitiba",
                // char(2) no banco: qualquer coisa fora de dois caracteres estoura na insercao.
                Uf = "PR",
                Pais = "BR"
            }
        };

        pedido.Itens.Add(new PedidoItem
        {
            IdVariacao = catalogo.IdVariacao,
            IdProduto = catalogo.IdProduto,
            SkuSnapshot = "VST-A-01",
            NomeProdutoSnapshot = "Vestido a",
            TamanhoSnapshot = "PP",
            CorSnapshot = "Preto",
            ImagemUrlSnapshot = "/media/vestido-a.jpg",
            Quantidade = 1,
            PrecoUnitarioCentavos = 24900,
            DescontoUnitarioCentavos = 0,
            PesoGramasSnapshot = 420,
            TotalLinhaCentavos = 24900
        });

        await contexto.Pedidos.AddAsync(pedido, cancellationToken);
        await contexto.SaveChangesAsync(cancellationToken);

        return pedido;
    }

    /// <summary>
    /// Evento de webhook cru. O Payload PRECISA ser JSON valido: a coluna e jsonb de verdade e
    /// o Postgres recusa texto solto — que e exatamente o que este projeto quer que aconteca.
    /// </summary>
    public static PagamentoEvento NovoEvento(string providerEventId, string tipo = "charge.paid") =>
        new()
        {
            IdPagamento = null,
            ProviderEventId = providerEventId,
            Tipo = tipo,
            Payload = $$"""{"id":"{{providerEventId}}","type":"{{tipo}}"}""",
            RecebidoEm = new DateTime(2026, 3, 1, 12, 0, 0, DateTimeKind.Utc)
        };
}
