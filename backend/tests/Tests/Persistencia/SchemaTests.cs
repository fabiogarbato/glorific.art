using Glorific.Tests.TestSupport;
using Xunit;

namespace Glorific.Tests.Persistencia;

/// <summary>
/// Guarda de schema: pergunta ao Postgres o que ele REALMENTE criou, e nao ao modelo do EF.
///
/// A diferenca importa. O ModelSnapshot descreve a intencao; o catalogo do banco descreve o que
/// a migration produziu. As duas coisas divergem silenciosamente quando alguem edita uma
/// configuration sem gerar migration, quando um Cascade nasce por convencao sem ninguem
/// escrever OnDelete, ou quando uma coluna jsonb vira text porque o HasColumnType sumiu num
/// merge. Cada teste aqui e uma regra de producao — nao decoracao de cobertura:
///
///   - CHECK: e a ultima linha de defesa contra estoque incoerente, nota fora de 1..5 e peca sem
///     dimensao (que o Melhor Envio recusa com 422).
///   - Indice PARCIAL: sem o filtro, o carrinho convertido de ontem impede o de hoje e o segundo
///     usuario sem CPF colide com o primeiro no NULL. SQLite aceitaria os dois casos.
///   - jsonb: guardar payload de gateway como text tira a unica ferramenta de investigacao que
///     sobra quando o parceiro muda contrato sem avisar.
///   - Cascade: um ON DELETE CASCADE onde nao devia significa apagar um usuario e levar o
///     historico de pedidos junto. Esta e a lista fechada de onde ele e permitido.
/// </summary>
[Collection(BancoCollection.Nome)]
public sealed class SchemaTests
{
    private readonly BancoFixture _banco;

    public SchemaTests(BancoFixture banco) => _banco = banco;

    /// <summary>
    /// Unicos filhos de agregado do modelo: linhas que nao tem sentido sem o pai. Qualquer FK
    /// Cascade fora desta lista e regressao, e o teste falha nomeando a intrusa.
    /// </summary>
    private static readonly string[] CascadePermitido =
    [
        "avaliacoes_midias.id_avaliacao",
        "carrinho_itens.id_carrinho",
        "envios_eventos.id_envio",
        "midias_produtos.id_produto",
        "pedido_itens.id_pedido",
        "produtos_colecoes.id_produto",
        "tabelas_medidas_linhas.id_tabela_medidas",
        "usuarios_roles.id_usuario"
    ];

    [Fact]
    public async Task Schema_ChecksDeIntegridade_ExistemNoBancoComAExpressaoEsperada()
    {
        var linhas = await _banco.ConsultarAsync(
            """
            SELECT c.conname, pg_get_constraintdef(c.oid)
            FROM pg_constraint c
            JOIN pg_class t ON t.oid = c.conrelid
            JOIN pg_namespace n ON n.oid = t.relnamespace
            WHERE c.contype = 'c' AND n.nspname = 'public'
            ORDER BY c.conname
            """);

        var definicoes = linhas.ToDictionary(
            linha => linha[0] ?? string.Empty,
            linha => linha[1] ?? string.Empty,
            StringComparer.Ordinal);

        // Estoque: reserva soft nunca pode passar do fisico, e nenhum dos dois pode ficar negativo.
        Assert.True(
            definicoes.ContainsKey("ck_estoques_variacoes_quantidades"),
            "CHECK ck_estoques_variacoes_quantidades ausente do banco.");
        Assert.Contains("quantidade_reservada <= quantidade", definicoes["ck_estoques_variacoes_quantidades"]);
        Assert.Contains("quantidade >= 0", definicoes["ck_estoques_variacoes_quantidades"]);

        // Avaliacao: nota fora de 1..5 quebra a media denormalizada da vitrine.
        Assert.True(
            definicoes.ContainsKey("ck_avaliacoes_nota"),
            "CHECK ck_avaliacoes_nota ausente do banco.");
        Assert.Contains("nota >= 1", definicoes["ck_avaliacoes_nota"]);
        Assert.Contains("nota <= 5", definicoes["ck_avaliacoes_nota"]);

        // Variacao: peso e dimensoes sao obrigatorios na cotacao do Melhor Envio.
        Assert.True(
            definicoes.ContainsKey("ck_produto_variacoes_dimensoes"),
            "CHECK ck_produto_variacoes_dimensoes ausente do banco.");

        // O pg_get_constraintdef normaliza a expressao e anota o cast das colunas numeric
        // (altura_cm > (0)::numeric), entao a assercao para no operador de proposito.
        var dimensoes = definicoes["ck_produto_variacoes_dimensoes"];
        Assert.Contains("peso_gramas > 0", dimensoes);
        Assert.Contains("altura_cm >", dimensoes);
        Assert.Contains("largura_cm >", dimensoes);
        Assert.Contains("comprimento_cm >", dimensoes);
    }

    [Fact]
    public async Task Schema_IndicesParciais_ExistemComOFiltroEsperado()
    {
        // indpred so vem preenchido em indice PARCIAL — e exatamente isso que se quer provar.
        var linhas = await _banco.ConsultarAsync(
            """
            SELECT i.relname, pg_get_expr(x.indpred, x.indrelid)
            FROM pg_index x
            JOIN pg_class i ON i.oid = x.indexrelid
            JOIN pg_class t ON t.oid = x.indrelid
            JOIN pg_namespace n ON n.oid = t.relnamespace
            WHERE x.indpred IS NOT NULL AND n.nspname = 'public'
            ORDER BY i.relname
            """);

        var filtros = linhas.ToDictionary(
            linha => linha[0] ?? string.Empty,
            linha => linha[1] ?? string.Empty,
            StringComparer.Ordinal);

        // CPF e opcional; quando existe, e unico. Sem o filtro o segundo usuario sem CPF colide.
        Assert.True(filtros.ContainsKey("ux_usuarios_cpf"), "ux_usuarios_cpf nao e um indice parcial.");
        Assert.Contains("cpf IS NOT NULL", filtros["ux_usuarios_cpf"]);

        // Um carrinho ABERTO por usuario e um por sessao anonima. StatusCarrinho.Aberto = 1.
        Assert.True(
            filtros.ContainsKey("ux_carrinhos_usuario_aberto"),
            "ux_carrinhos_usuario_aberto nao e um indice parcial.");
        Assert.Contains("status = 1", filtros["ux_carrinhos_usuario_aberto"]);

        Assert.True(
            filtros.ContainsKey("ux_carrinhos_chave_sessao_aberto"),
            "ux_carrinhos_chave_sessao_aberto nao e um indice parcial.");
        Assert.Contains("status = 1", filtros["ux_carrinhos_chave_sessao_aberto"]);

        // Os tres sao unicos: um indice parcial nao-unico nao impediria a duplicata.
        var unicosParciais = await _banco.ConsultarColunaAsync(
            """
            SELECT i.relname
            FROM pg_index x
            JOIN pg_class i ON i.oid = x.indexrelid
            JOIN pg_class t ON t.oid = x.indrelid
            JOIN pg_namespace n ON n.oid = t.relnamespace
            WHERE x.indpred IS NOT NULL AND x.indisunique AND n.nspname = 'public'
            ORDER BY i.relname
            """);

        Assert.Contains("ux_usuarios_cpf", unicosParciais);
        Assert.Contains("ux_carrinhos_usuario_aberto", unicosParciais);
        Assert.Contains("ux_carrinhos_chave_sessao_aberto", unicosParciais);
    }

    [Fact]
    public async Task Schema_ColunasDePayloadCru_SaoJsonbENaoText()
    {
        var colunas = await _banco.ConsultarColunaAsync(
            """
            SELECT table_name || '.' || column_name
            FROM information_schema.columns
            WHERE table_schema = 'public' AND data_type = 'jsonb'
            ORDER BY 1
            """);

        // Payload cru do gateway e da transportadora: quando o parceiro muda contrato sem avisar,
        // e a unica forma de reconstruir o que aconteceu sem pedir log ao suporte deles.
        Assert.Contains("pagamentos_eventos.payload", colunas);
        Assert.Contains("pagamentos.raw_ultima_resposta", colunas);
        Assert.Contains("envios.raw_ultima_resposta", colunas);
    }

    [Fact]
    public async Task Schema_ChavesEstrangeiras_SoUsamCascadeNaListaPermitida()
    {
        // confdeltype = 'c' e ON DELETE CASCADE no catalogo do Postgres.
        var cascades = await _banco.ConsultarColunaAsync(
            """
            SELECT t.relname || '.' || a.attname
            FROM pg_constraint c
            JOIN pg_class t ON t.oid = c.conrelid
            JOIN pg_namespace n ON n.oid = t.relnamespace
            JOIN LATERAL unnest(c.conkey) AS k(attnum) ON true
            JOIN pg_attribute a ON a.attrelid = t.oid AND a.attnum = k.attnum
            WHERE c.contype = 'f' AND c.confdeltype = 'c' AND n.nspname = 'public'
            ORDER BY 1
            """);

        var intrusas = cascades.Except(CascadePermitido, StringComparer.Ordinal).ToArray();

        Assert.True(
            intrusas.Length == 0,
            "FK com ON DELETE CASCADE fora da lista permitida: " + string.Join(", ", intrusas));

        // E a mao inversa: se um Cascade legitimo virar Restrict por acidente, o filho de
        // agregado passa a impedir a exclusao do pai e a tela quebra sem ninguem entender.
        var faltantes = CascadePermitido.Except(cascades, StringComparer.Ordinal).ToArray();

        Assert.True(
            faltantes.Length == 0,
            "FK que deveria ser ON DELETE CASCADE nao esta mais: " + string.Join(", ", faltantes));
    }

    [Fact]
    public async Task Schema_ColunasDeDataHora_SaoTimestampSemFusoHorario()
    {
        // O projeto inteiro roda com Npgsql.EnableLegacyTimestampBehavior e grava DateTime Kind=Utc
        // em "timestamp without time zone". Uma coluna timestamptz solta faz a data voltar
        // deslocada do fuso do servidor — o bug do token de 8 h que valia 5 h.
        var comFuso = await _banco.ConsultarColunaAsync(
            """
            SELECT table_name || '.' || column_name
            FROM information_schema.columns
            WHERE table_schema = 'public' AND data_type = 'timestamp with time zone'
            ORDER BY 1
            """);

        Assert.True(
            comFuso.Count == 0,
            "Coluna timestamptz encontrada: " + string.Join(", ", comFuso));
    }

    [Fact]
    public async Task Schema_TabelaDeEnvios_ExpoeOXminUsadoComoTokenDeConcorrencia()
    {
        // xmin e coluna de sistema: nao aparece em information_schema, so em pg_attribute com
        // attnum negativo. E o que resolve a corrida entre o EnvioProcessor e a contratacao
        // manual do admin sobre o mesmo envio.
        var xmin = await _banco.ConsultarColunaAsync(
            """
            SELECT a.attname
            FROM pg_attribute a
            JOIN pg_class t ON t.oid = a.attrelid
            JOIN pg_namespace n ON n.oid = t.relnamespace
            WHERE n.nspname = 'public' AND t.relname = 'envios' AND a.attname = 'xmin'
            """);

        Assert.Single(xmin);
    }
}
