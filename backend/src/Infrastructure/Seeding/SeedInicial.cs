using Glorific.Domain.Constants;
using Glorific.Domain.Entities.Catalogo;
using Glorific.Domain.Entities.Config;
using Glorific.Domain.Entities.Estoque;
using Glorific.Domain.Entities.Identidade;
using Glorific.Domain.Enums;
using Glorific.Domain.Helpers;
using Glorific.Domain.ReferenceData;
using Glorific.Infrastructure.Data;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

namespace Glorific.Infrastructure.Seeding;

/// <summary>
/// Dado de referencia que o sistema PRECISA para funcionar: papeis, tipos de movimento de
/// estoque, a linha unica de configuracao da loja, a grade de tamanhos e as cores base.
///
/// Por que aqui e nao em HasData: HasData entra no ModelSnapshot, entao qualquer ajuste de
/// dado vira migration pendente e, pior, o EF passa a querer DELETE de linhas que o admin
/// editou pelo painel. Seed idempotente resolve os dois: roda em todo boot, insere so o que
/// falta e nunca sobrescreve o que ja existe.
///
/// IDEMPOTENCIA: cada bloco consulta o que ja esta la (pela mesma chave que tem UNIQUE no
/// banco) e insere apenas o complemento. Rodar dez vezes seguidas produz o mesmo estado.
///
/// NAO cria usuario admin. Senha de admin vem de variavel de ambiente, em seeder proprio.
/// </summary>
public static class SeedInicial
{
    /// <summary>
    /// Nasce VAZIO de proposito. Um placeholder tipo 00000000 nao e vazio, entao encobriria o
    /// Frete:CepOrigem vindo do ambiente e a loja ficaria sem cotar frete mesmo com o deploy
    /// configurado certo. Vazio deixa o fallback funcionar ate o admin preencher no painel.
    /// </summary>
    private const string CepOrigemProvisorio = "";

    public static async Task ExecutarAsync(
        GlorificContext contexto,
        ILogger logger,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(contexto);
        ArgumentNullException.ThrowIfNull(logger);

        var inseridos = 0;

        inseridos += await SemearRolesAsync(contexto, cancellationToken);
        inseridos += await SemearMovimentosEstoqueAsync(contexto, cancellationToken);
        inseridos += await SemearConfiguracaoLojaAsync(contexto, cancellationToken);
        inseridos += await SemearTamanhosAsync(contexto, cancellationToken);
        inseridos += await SemearCoresAsync(contexto, cancellationToken);

        if (inseridos == 0)
        {
            logger.LogInformation("Seed inicial: nada a inserir, dados de referencia ja presentes.");
            return;
        }

        await contexto.SaveChangesAsync(cancellationToken);
        logger.LogInformation("Seed inicial: {Quantidade} registros de referencia inseridos.", inseridos);
    }

    // ------------------------------------------------------------------
    // Papeis
    // ------------------------------------------------------------------

    /// <summary>
    /// Os quatro papeis do sistema. Sao linhas de tabela justamente para que a claim role do
    /// JWT nunca seja string livre digitada num campo de usuario.
    /// </summary>
    private static async Task<int> SemearRolesAsync(GlorificContext contexto, CancellationToken cancellationToken)
    {
        var existentes = await contexto.Roles
            .AsNoTracking()
            .Select(role => role.Nome)
            .ToListAsync(cancellationToken);

        var descricoes = new Dictionary<string, string>(StringComparer.Ordinal)
        {
            [Roles.Admin] = "Acesso total: usuarios, papeis, segredos e configuracao da loja.",
            [Roles.Gerente] = "Catalogo, estoque, precos, cupons e moderacao de avaliacoes.",
            [Roles.Operador] = "Expedicao: pedidos, etiquetas e rastreio.",
            [Roles.Cliente] = "Comprador da loja. Papel padrao de todo cadastro publico."
        };

        var faltantes = Roles.Todos
            .Where(nome => !existentes.Contains(nome, StringComparer.Ordinal))
            .Select(nome => new Role { Nome = nome, Descricao = descricoes[nome] })
            .ToList();

        if (faltantes.Count > 0)
            await contexto.Roles.AddRangeAsync(faltantes, cancellationToken);

        return faltantes.Count;
    }

    // ------------------------------------------------------------------
    // Movimentos de estoque
    // ------------------------------------------------------------------

    /// <summary>
    /// Os nove tipos de movimento. O Id inteiro nunca aparece no codigo de negocio: e resolvido
    /// por chave (MovimentoEstoqueKeys), e por isso o seed pode rodar em qualquer ordem.
    ///
    /// Sinal: +1 entra no fisico, -1 sai do fisico, 0 nao mexe no fisico.
    /// Reserva e liberacao existem como movimento proprio de sinal zero porque mexem apenas no
    /// reservado — sem registra-las, o ledger nao explica por que o disponivel caiu.
    /// Ajuste de inventario tambem e zero: a direcao dele vem da quantidade assinada do
    /// lancamento, nao do tipo.
    /// </summary>
    private static async Task<int> SemearMovimentosEstoqueAsync(
        GlorificContext contexto,
        CancellationToken cancellationToken)
    {
        var catalogo = new (MovimentoEstoqueKey Chave, int Sinal, string Descricao)[]
        {
            (MovimentoEstoqueKeys.CadastroInicial, +1, "Primeira carga da variacao no sistema."),
            (MovimentoEstoqueKeys.Reabastecimento, +1, "Compra de reposicao junto ao fornecedor ou producao."),
            (MovimentoEstoqueKeys.ReservaCheckout, 0, "Incrementa apenas a quantidade reservada."),
            (MovimentoEstoqueKeys.LiberacaoReserva, 0, "Devolve a reserva de um pagamento expirado ou cancelado."),
            (MovimentoEstoqueKeys.VendaSistema, -1, "Pagamento confirmado: baixa o fisico e zera a reserva."),
            (MovimentoEstoqueKeys.VendaManual, -1, "Venda registrada fora da loja, lancada pelo admin."),
            (MovimentoEstoqueKeys.DevolucaoCliente, +1, "Peca voltou do cliente e foi aprovada para revenda."),
            (MovimentoEstoqueKeys.AjusteInventario, 0, "Correcao de contagem apos inventario fisico."),
            (MovimentoEstoqueKeys.PerdaAvaria, -1, "Peca danificada, extraviada ou descartada.")
        };

        var existentes = await contexto.MovimentosEstoque
            .AsNoTracking()
            .Select(movimento => movimento.Nome)
            .ToListAsync(cancellationToken);

        var faltantes = catalogo
            .Where(item => !existentes.Contains(item.Chave.Value, StringComparer.Ordinal))
            .Select(item => new MovimentoEstoque
            {
                Nome = item.Chave.Value,
                Sinal = item.Sinal,
                Descricao = item.Descricao
            })
            .ToList();

        if (faltantes.Count > 0)
            await contexto.MovimentosEstoque.AddRangeAsync(faltantes, cancellationToken);

        return faltantes.Count;
    }

    // ------------------------------------------------------------------
    // Configuracao da loja
    // ------------------------------------------------------------------

    /// <summary>
    /// A tabela configuracoes_loja tem UMA linha por design. O seed so a cria quando nao existe:
    /// sobrescrever aqui apagaria o que o admin acabou de configurar no painel a cada deploy.
    /// </summary>
    private static async Task<int> SemearConfiguracaoLojaAsync(
        GlorificContext contexto,
        CancellationToken cancellationToken)
    {
        if (await contexto.ConfiguracoesLoja.AnyAsync(cancellationToken))
            return 0;

        await contexto.ConfiguracoesLoja.AddAsync(
            new ConfiguracaoLoja
            {
                CepOrigem = CepOrigemProvisorio,
                PrazoManuseioDias = 2,
                PoliticaTrocaDias = 7,
                FreteGratisAcimaDeCentavos = null,
                PedidoMinimoCentavos = null,
                ExibirEstoqueBaixo = false,
                LimiteEstoqueBaixo = 3
            },
            cancellationToken);

        return 1;
    }

    // ------------------------------------------------------------------
    // Grade de tamanhos
    // ------------------------------------------------------------------

    /// <summary>
    /// Grade alfa, grade numerica e tamanho unico.
    ///
    /// Ordem e explicita e espacada de dez em dez: sem ela "GG" ordena antes de "P" e o seletor
    /// da pagina de produto sai errado; espacada porque inserir "XXG" entre GG e XG depois nao
    /// pode obrigar a renumerar a tabela inteira.
    /// A unicidade no banco e por (grade, codigo) — "38" pode existir na numerica e na infantil.
    /// </summary>
    private static async Task<int> SemearTamanhosAsync(GlorificContext contexto, CancellationToken cancellationToken)
    {
        var catalogo = new List<(GradeTamanho Grade, string Codigo, string Descricao, int Ordem)>
        {
            (GradeTamanho.Alfa, "PP", "Extra pequeno", 10),
            (GradeTamanho.Alfa, "P", "Pequeno", 20),
            (GradeTamanho.Alfa, "M", "Medio", 30),
            (GradeTamanho.Alfa, "G", "Grande", 40),
            (GradeTamanho.Alfa, "GG", "Extra grande", 50),
            (GradeTamanho.Alfa, "XG", "Extra grande plus", 60)
        };

        // Grade numerica brasileira de moda feminina: pares de 36 a 46.
        var ordemNumerica = 100;
        for (var numero = 36; numero <= 46; numero += 2)
        {
            catalogo.Add((GradeTamanho.Numerica, numero.ToString(), $"Numero {numero}", ordemNumerica));
            ordemNumerica += 10;
        }

        catalogo.Add((GradeTamanho.Unico, "Unico", "Tamanho unico", 900));

        var existentes = await contexto.Tamanhos
            .AsNoTracking()
            .Select(tamanho => new { tamanho.Grade, tamanho.Codigo })
            .ToListAsync(cancellationToken);

        var chavesExistentes = existentes
            .Select(item => $"{(int)item.Grade}|{item.Codigo}")
            .ToHashSet(StringComparer.Ordinal);

        var faltantes = catalogo
            .Where(item => !chavesExistentes.Contains($"{(int)item.Grade}|{item.Codigo}"))
            .Select(item => new Tamanho
            {
                Grade = item.Grade,
                Codigo = item.Codigo,
                Descricao = item.Descricao,
                Ordem = item.Ordem,
                Ativo = true
            })
            .ToList();

        if (faltantes.Count > 0)
            await contexto.Tamanhos.AddRangeAsync(faltantes, cancellationToken);

        return faltantes.Count;
    }

    // ------------------------------------------------------------------
    // Cores base
    // ------------------------------------------------------------------

    /// <summary>
    /// Um punhado de cores para a loja nao nascer sem swatch nenhum. Nao e a paleta definitiva:
    /// o admin cadastra as cores reais de cada colecao. O slug e a chave unica no banco e sai
    /// do SlugHelper, o mesmo usado no resto do catalogo, para "Verde Oliva" e "verde-oliva"
    /// nunca virarem duas linhas.
    /// </summary>
    private static async Task<int> SemearCoresAsync(GlorificContext contexto, CancellationToken cancellationToken)
    {
        var catalogo = new (string Nome, string Hex, int Ordem)[]
        {
            ("Preto", "#000000", 10),
            ("Branco", "#FFFFFF", 20),
            ("Off White", "#F3EFE7", 30),
            ("Bege", "#D9C7A8", 40),
            ("Terracota", "#B75C3C", 50),
            ("Verde Oliva", "#6B7A4F", 60),
            ("Azul Marinho", "#1F2A44", 70),
            ("Vinho", "#6E1B2B", 80),
            ("Cinza Mescla", "#9A9A9A", 90),
            ("Rosa Antigo", "#C98B92", 100)
        };

        var existentes = await contexto.Cores
            .AsNoTracking()
            .Select(cor => cor.Slug)
            .ToListAsync(cancellationToken);

        var faltantes = catalogo
            .Select(item => new { item.Nome, item.Hex, item.Ordem, Slug = SlugHelper.Gerar(item.Nome) })
            .Where(item => !existentes.Contains(item.Slug, StringComparer.Ordinal))
            .Select(item => new Cor
            {
                Nome = item.Nome,
                Slug = item.Slug,
                HexRgb = item.Hex,
                Ordem = item.Ordem,
                Ativo = true
            })
            .ToList();

        if (faltantes.Count > 0)
            await contexto.Cores.AddRangeAsync(faltantes, cancellationToken);

        return faltantes.Count;
    }
}
