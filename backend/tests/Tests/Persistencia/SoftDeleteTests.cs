using Glorific.Domain.Entities.Catalogo;
using Glorific.Domain.Enums;
using Glorific.Domain.Interfaces.Repositories;
using Glorific.Infrastructure.Data;
using Glorific.Tests.TestSupport;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Npgsql;
using Xunit;

namespace Glorific.Tests.Persistencia;

/// <summary>
/// Soft delete de produto: some da vitrine, sobrevive no historico, e continua ocupando o slug.
///
/// As tres coisas juntas sao o ponto. O filtro global do EF resolve a primeira e cria as outras
/// duas armadilhas:
///
///   (a) o recibo de dois anos atras aponta para um produto desativado; com o filtro ligado a
///       navegacao obrigatoria vem nula e a tela de detalhe abre sem as linhas. Por isso o
///       PedidoRepository consulta SEMPRE com IgnoreQueryFilters.
///   (b) o indice unico do Postgres nao conhece filtro de consulta nenhum. Produto desativado
///       continua ocupando slug e sku_base, e a validacao de cadastro que le sem
///       IgnoreQueryFilters aprova o duplicado e explode com 23505 no SaveChanges.
///
/// Nada disso aparece contra banco em memoria — o (b) so existe porque ha um UNIQUE de verdade.
/// </summary>
[Collection(BancoCollection.Nome)]
public sealed class SoftDeleteTests : IAsyncLifetime
{
    private readonly BancoFixture _banco;

    public SoftDeleteTests(BancoFixture banco) => _banco = banco;

    public Task InitializeAsync() => _banco.LimparAsync();

    public Task DisposeAsync() => Task.CompletedTask;

    [Fact]
    public async Task Catalogo_ProdutoDesativado_SomeDoFiltroGlobalMasContinuaNoBanco()
    {
        await using var contexto = _banco.CriarContexto();

        var catalogo = await DadosPersistencia.CriarCatalogoComEstoqueAsync(contexto, quantidade: 5);
        await DesativarProdutoAsync(contexto, catalogo.IdProduto);

        // Consulta normal: o produto simplesmente nao existe para a loja.
        Assert.False(await contexto.Produtos.AsNoTracking().AnyAsync(p => p.Id == catalogo.IdProduto));

        // Escape hatch do painel administrativo: a linha continua la, inteira.
        var desativado = await contexto.Produtos
            .AsNoTracking()
            .IgnoreQueryFilters()
            .SingleAsync(p => p.Id == catalogo.IdProduto);

        Assert.False(desativado.Ativo);
        Assert.Equal("vestido-a", desativado.Slug);
    }

    [Fact]
    public async Task QueryDisponiveis_ProdutoDesativadoComEstoque_NaoVoltaNaVitrine()
    {
        await using var contexto = _banco.CriarContexto();

        var ativo = await DadosPersistencia.CriarCatalogoComEstoqueAsync(
            contexto, quantidade: 5, sufixo: "ativo");
        var inativo = await DadosPersistencia.CriarCatalogoComEstoqueAsync(
            contexto, quantidade: 5, sufixo: "inativo");

        await DesativarProdutoAsync(contexto, inativo.IdProduto);

        using var escopo = _banco.CriarEscopo();
        var repositorio = escopo.ServiceProvider.GetRequiredService<IProdutoRepository>();

        var ids = await repositorio.QueryDisponiveis().Select(p => p.Id).ToListAsync();

        // Tem estoque, mas esta desativado: a vitrine nao pode oferecer.
        Assert.Contains(ativo.IdProduto, ids);
        Assert.DoesNotContain(inativo.IdProduto, ids);
    }

    [Fact]
    public async Task ObterCompletoAsync_ProdutoDesativado_ContinuaVisivelNoHistoricoDoPedido()
    {
        int idPedido;
        int idProduto;

        await using (var preparacao = _banco.CriarContexto())
        {
            var catalogo = await DadosPersistencia.CriarCatalogoComEstoqueAsync(preparacao, quantidade: 5);
            var usuario = await DadosPersistencia.CriarUsuarioAsync(preparacao);
            var pedido = await DadosPersistencia.CriarPedidoComItemAsync(
                preparacao, usuario.Id, catalogo, "GA-2026-000001");

            idPedido = pedido.Id;
            idProduto = catalogo.IdProduto;

            // O admin tira a peca do ar DEPOIS de o pedido existir. E o caso real.
            await DesativarProdutoAsync(preparacao, idProduto);
        }

        using var escopo = _banco.CriarEscopo();
        var repositorio = escopo.ServiceProvider.GetRequiredService<IPedidoRepository>();

        var pedidoCompleto = await repositorio.ObterCompletoAsync(idPedido);

        Assert.NotNull(pedidoCompleto);

        var item = Assert.Single(pedidoCompleto.Itens);

        // A navegacao carregou apesar do filtro de soft delete — este e o teste do
        // IgnoreQueryFilters no repositorio de pedidos.
        Assert.NotNull(item.Produto);
        Assert.Equal(idProduto, item.Produto.Id);
        Assert.False(item.Produto.Ativo);

        // E o recibo continua sendo o SNAPSHOT, nao a navegacao: renomear o produto nao reescreve
        // pedido antigo.
        Assert.Equal("VST-A-01", item.SkuSnapshot);
        Assert.Equal("Vestido a", item.NomeProdutoSnapshot);
        Assert.Equal(24900, item.PrecoUnitarioCentavos);
    }

    [Fact]
    public async Task SlugEmUsoAsync_ProdutoDesativado_ContinuaOcupandoOSlug()
    {
        await using var contexto = _banco.CriarContexto();

        var catalogo = await DadosPersistencia.CriarCatalogoComEstoqueAsync(contexto, quantidade: 1);
        await DesativarProdutoAsync(contexto, catalogo.IdProduto);

        using var escopo = _banco.CriarEscopo();
        var repositorio = escopo.ServiceProvider.GetRequiredService<IProdutoRepository>();

        // A validacao de cadastro tem que enxergar o desativado, senao aprova o duplicado.
        Assert.True(await repositorio.SlugEmUsoAsync("vestido-a"));
        Assert.True(await repositorio.SkuBaseEmUsoAsync("VST-A"));
    }

    [Fact]
    public async Task Insercao_SlugDeProdutoDesativado_ERejeitadaPeloIndiceUnicoDoBanco()
    {
        await using var contexto = _banco.CriarContexto();

        var catalogo = await DadosPersistencia.CriarCatalogoComEstoqueAsync(contexto, quantidade: 1);
        await DesativarProdutoAsync(contexto, catalogo.IdProduto);

        // sku_base diferente de proposito: assim a violacao so pode vir do slug.
        var duplicado = new Produto
        {
            Nome = "Vestido novo, slug velho",
            Slug = "vestido-a",
            SkuBase = "VST-OUTRO",
            IdCategoria = catalogo.IdCategoria,
            Genero = GeneroProduto.Feminino,
            PrecoBaseCentavos = 19900,
            Ativo = true
        };

        await contexto.Produtos.AddAsync(duplicado);

        var excecao = await Assert.ThrowsAsync<DbUpdateException>(
            async () => await contexto.SaveChangesAsync());

        var postgres = Assert.IsType<PostgresException>(excecao.InnerException);

        // 23505 = unique_violation. O indice nao conhece soft delete, e e essa a rede.
        Assert.Equal("23505", postgres.SqlState);
        Assert.Equal("ux_produtos_slug", postgres.ConstraintName);
    }

    [Fact]
    public async Task Exclusao_ProdutoComVariacaoEItemDePedido_ERejeitadaPelasFksRestrict()
    {
        int idProduto;

        await using (var preparacao = _banco.CriarContexto())
        {
            var catalogo = await DadosPersistencia.CriarCatalogoComEstoqueAsync(preparacao, quantidade: 5);
            var usuario = await DadosPersistencia.CriarUsuarioAsync(preparacao);
            await DadosPersistencia.CriarPedidoComItemAsync(
                preparacao, usuario.Id, catalogo, "GA-2026-000002");

            idProduto = catalogo.IdProduto;
        }

        // A razao de o soft delete existir: apagar de verdade levaria o historico junto. As FKs
        // que apontam para produtos (produto_variacoes, pedido_itens, logs, avaliacoes) sao
        // todas Restrict, e o banco recusa o DELETE antes de qualquer regra do C# opinar.
        var excecao = await Assert.ThrowsAsync<PostgresException>(async () => await _banco.ExecutarAsync(
            $"DELETE FROM public.produtos WHERE id = {idProduto}"));

        // 23503 = foreign_key_violation.
        Assert.Equal("23503", excecao.SqlState);
    }

    /// <summary>
    /// Desativa via UPDATE direto com IgnoreQueryFilters. Carregar a entidade e alterar o Ativo
    /// funcionaria, mas obrigaria o teste a lidar com o filtro global na hora de reencontra-la.
    /// </summary>
    private static Task DesativarProdutoAsync(GlorificContext contexto, int idProduto) =>
        contexto.Produtos
            .IgnoreQueryFilters()
            .Where(p => p.Id == idProduto)
            .ExecuteUpdateAsync(s => s.SetProperty(p => p.Ativo, false));
}
