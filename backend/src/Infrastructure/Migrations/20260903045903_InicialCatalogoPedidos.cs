using System;
using Microsoft.EntityFrameworkCore.Migrations;
using Npgsql.EntityFrameworkCore.PostgreSQL.Metadata;

#nullable disable

namespace Glorific.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class InicialCatalogoPedidos : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "app_secrets",
                columns: table => new
                {
                    id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    config_key = table.Column<string>(type: "character varying(120)", maxLength: 120, nullable: false),
                    valor_criptografado = table.Column<string>(type: "text", nullable: false),
                    eh_segredo = table.Column<bool>(type: "boolean", nullable: false, defaultValue: true),
                    descricao = table.Column<string>(type: "character varying(255)", maxLength: 255, nullable: true),
                    data_criacao = table.Column<DateTime>(type: "timestamp without time zone", nullable: false),
                    data_alteracao = table.Column<DateTime>(type: "timestamp without time zone", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_app_secrets", x => x.id);
                });

            migrationBuilder.CreateTable(
                name: "configuracoes_loja",
                columns: table => new
                {
                    id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    frete_gratis_acima_de_centavos = table.Column<int>(type: "integer", nullable: true),
                    prazo_manuseio_dias = table.Column<int>(type: "integer", nullable: false, defaultValue: 2),
                    cep_origem = table.Column<string>(type: "character varying(8)", maxLength: 8, nullable: false),
                    politica_troca_dias = table.Column<int>(type: "integer", nullable: false, defaultValue: 7),
                    pedido_minimo_centavos = table.Column<int>(type: "integer", nullable: true),
                    exibir_estoque_baixo = table.Column<bool>(type: "boolean", nullable: false, defaultValue: false),
                    limite_estoque_baixo = table.Column<int>(type: "integer", nullable: false, defaultValue: 3),
                    data_criacao = table.Column<DateTime>(type: "timestamp without time zone", nullable: false),
                    data_alteracao = table.Column<DateTime>(type: "timestamp without time zone", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_configuracoes_loja", x => x.id);
                });

            migrationBuilder.CreateTable(
                name: "midias",
                columns: table => new
                {
                    id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    url = table.Column<string>(type: "text", nullable: false),
                    public_id = table.Column<string>(type: "character varying(255)", maxLength: 255, nullable: true),
                    alt_text = table.Column<string>(type: "character varying(255)", maxLength: 255, nullable: true),
                    largura = table.Column<int>(type: "integer", nullable: true),
                    altura = table.Column<int>(type: "integer", nullable: true),
                    tamanho_bytes = table.Column<long>(type: "bigint", nullable: true),
                    content_type = table.Column<string>(type: "character varying(120)", maxLength: 120, nullable: true),
                    data_criacao = table.Column<DateTime>(type: "timestamp without time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_midias", x => x.id);
                });

            migrationBuilder.CreateTable(
                name: "movimentos_estoque",
                columns: table => new
                {
                    id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    nome = table.Column<string>(type: "character varying(60)", maxLength: 60, nullable: false),
                    descricao = table.Column<string>(type: "character varying(255)", maxLength: 255, nullable: true),
                    sinal = table.Column<int>(type: "integer", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_movimentos_estoque", x => x.id);
                });

            migrationBuilder.CreateTable(
                name: "roles",
                columns: table => new
                {
                    id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    nome = table.Column<string>(type: "character varying(40)", maxLength: 40, nullable: false),
                    descricao = table.Column<string>(type: "character varying(255)", maxLength: 255, nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_roles", x => x.id);
                });

            migrationBuilder.CreateTable(
                name: "tabelas_medidas",
                columns: table => new
                {
                    id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    nome = table.Column<string>(type: "character varying(120)", maxLength: 120, nullable: false),
                    observacao = table.Column<string>(type: "text", nullable: true),
                    ativo = table.Column<bool>(type: "boolean", nullable: false, defaultValue: true),
                    data_criacao = table.Column<DateTime>(type: "timestamp without time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_tabelas_medidas", x => x.id);
                });

            migrationBuilder.CreateTable(
                name: "tamanhos",
                columns: table => new
                {
                    id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    codigo = table.Column<string>(type: "character varying(10)", maxLength: 10, nullable: false),
                    descricao = table.Column<string>(type: "character varying(120)", maxLength: 120, nullable: true),
                    ordem = table.Column<int>(type: "integer", nullable: false),
                    grade = table.Column<int>(type: "integer", nullable: false),
                    ativo = table.Column<bool>(type: "boolean", nullable: false, defaultValue: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_tamanhos", x => x.id);
                });

            migrationBuilder.CreateTable(
                name: "usuarios",
                columns: table => new
                {
                    id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    uuid = table.Column<string>(type: "character varying(36)", maxLength: 36, nullable: false),
                    email = table.Column<string>(type: "character varying(255)", maxLength: 255, nullable: false),
                    email_verificado = table.Column<bool>(type: "boolean", nullable: false, defaultValue: false),
                    nome_completo = table.Column<string>(type: "character varying(180)", maxLength: 180, nullable: true),
                    cpf = table.Column<string>(type: "character varying(11)", maxLength: 11, nullable: true),
                    telefone = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: true),
                    senha_hash = table.Column<string>(type: "character varying(255)", maxLength: 255, nullable: true),
                    foto_url = table.Column<string>(type: "text", nullable: true),
                    data_nascimento = table.Column<DateTime>(type: "timestamp without time zone", nullable: true),
                    aceita_marketing = table.Column<bool>(type: "boolean", nullable: false, defaultValue: false),
                    ativo = table.Column<bool>(type: "boolean", nullable: false, defaultValue: true),
                    ultimo_login_em = table.Column<DateTime>(type: "timestamp without time zone", nullable: true),
                    data_criacao = table.Column<DateTime>(type: "timestamp without time zone", nullable: false),
                    data_alteracao = table.Column<DateTime>(type: "timestamp without time zone", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_usuarios", x => x.id);
                });

            migrationBuilder.CreateTable(
                name: "categorias",
                columns: table => new
                {
                    id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    nome = table.Column<string>(type: "character varying(120)", maxLength: 120, nullable: false),
                    slug = table.Column<string>(type: "character varying(140)", maxLength: 140, nullable: false),
                    descricao = table.Column<string>(type: "text", nullable: true),
                    id_categoria_pai = table.Column<int>(type: "integer", nullable: true),
                    id_midia_capa = table.Column<int>(type: "integer", nullable: true),
                    ordem = table.Column<int>(type: "integer", nullable: false, defaultValue: 0),
                    habilitado = table.Column<bool>(type: "boolean", nullable: false, defaultValue: true),
                    meta_title = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: true),
                    meta_description = table.Column<string>(type: "character varying(400)", maxLength: 400, nullable: true),
                    data_criacao = table.Column<DateTime>(type: "timestamp without time zone", nullable: false),
                    data_alteracao = table.Column<DateTime>(type: "timestamp without time zone", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_categorias", x => x.id);
                    table.ForeignKey(
                        name: "FK_categorias_categorias_id_categoria_pai",
                        column: x => x.id_categoria_pai,
                        principalTable: "categorias",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_categorias_midias_id_midia_capa",
                        column: x => x.id_midia_capa,
                        principalTable: "midias",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "colecoes",
                columns: table => new
                {
                    id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    nome = table.Column<string>(type: "character varying(120)", maxLength: 120, nullable: false),
                    slug = table.Column<string>(type: "character varying(140)", maxLength: 140, nullable: false),
                    descricao = table.Column<string>(type: "text", nullable: true),
                    epigrafe = table.Column<string>(type: "text", nullable: true),
                    id_midia_capa = table.Column<int>(type: "integer", nullable: true),
                    id_midia_banner = table.Column<int>(type: "integer", nullable: true),
                    data_inicio = table.Column<DateTime>(type: "timestamp without time zone", nullable: true),
                    data_fim = table.Column<DateTime>(type: "timestamp without time zone", nullable: true),
                    destaque = table.Column<bool>(type: "boolean", nullable: false, defaultValue: false),
                    habilitado = table.Column<bool>(type: "boolean", nullable: false, defaultValue: true),
                    ordem = table.Column<int>(type: "integer", nullable: false, defaultValue: 0),
                    data_criacao = table.Column<DateTime>(type: "timestamp without time zone", nullable: false),
                    data_alteracao = table.Column<DateTime>(type: "timestamp without time zone", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_colecoes", x => x.id);
                    table.ForeignKey(
                        name: "FK_colecoes_midias_id_midia_banner",
                        column: x => x.id_midia_banner,
                        principalTable: "midias",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_colecoes_midias_id_midia_capa",
                        column: x => x.id_midia_capa,
                        principalTable: "midias",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "cores",
                columns: table => new
                {
                    id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    nome = table.Column<string>(type: "character varying(80)", maxLength: 80, nullable: false),
                    slug = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false),
                    hex_rgb = table.Column<string>(type: "character varying(7)", maxLength: 7, nullable: false),
                    id_midia_swatch = table.Column<int>(type: "integer", nullable: true),
                    ordem = table.Column<int>(type: "integer", nullable: false, defaultValue: 0),
                    ativo = table.Column<bool>(type: "boolean", nullable: false, defaultValue: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_cores", x => x.id);
                    table.ForeignKey(
                        name: "FK_cores_midias_id_midia_swatch",
                        column: x => x.id_midia_swatch,
                        principalTable: "midias",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "tabelas_medidas_linhas",
                columns: table => new
                {
                    id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    id_tabela_medidas = table.Column<int>(type: "integer", nullable: false),
                    id_tamanho = table.Column<int>(type: "integer", nullable: false),
                    busto_cm = table.Column<decimal>(type: "numeric(8,2)", precision: 8, scale: 2, nullable: true),
                    cintura_cm = table.Column<decimal>(type: "numeric(8,2)", precision: 8, scale: 2, nullable: true),
                    quadril_cm = table.Column<decimal>(type: "numeric(8,2)", precision: 8, scale: 2, nullable: true),
                    comprimento_cm = table.Column<decimal>(type: "numeric(8,2)", precision: 8, scale: 2, nullable: true),
                    manga_cm = table.Column<decimal>(type: "numeric(8,2)", precision: 8, scale: 2, nullable: true),
                    ordem = table.Column<int>(type: "integer", nullable: false, defaultValue: 0)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_tabelas_medidas_linhas", x => x.id);
                    table.ForeignKey(
                        name: "FK_tabelas_medidas_linhas_tabelas_medidas_id_tabela_medidas",
                        column: x => x.id_tabela_medidas,
                        principalTable: "tabelas_medidas",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_tabelas_medidas_linhas_tamanhos_id_tamanho",
                        column: x => x.id_tamanho,
                        principalTable: "tamanhos",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "enderecos",
                columns: table => new
                {
                    id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    id_usuario = table.Column<int>(type: "integer", nullable: false),
                    apelido = table.Column<string>(type: "character varying(60)", maxLength: 60, nullable: true),
                    destinatario = table.Column<string>(type: "character varying(180)", maxLength: 180, nullable: false),
                    documento_destinatario = table.Column<string>(type: "character varying(14)", maxLength: 14, nullable: true),
                    telefone_contato = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false),
                    cep = table.Column<string>(type: "character varying(8)", maxLength: 8, nullable: false),
                    logradouro = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                    numero = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false),
                    complemento = table.Column<string>(type: "character varying(120)", maxLength: 120, nullable: true),
                    bairro = table.Column<string>(type: "character varying(120)", maxLength: 120, nullable: false),
                    cidade = table.Column<string>(type: "character varying(120)", maxLength: 120, nullable: false),
                    uf = table.Column<string>(type: "char(2)", nullable: false),
                    pais = table.Column<string>(type: "character varying(2)", maxLength: 2, nullable: false, defaultValue: "BR"),
                    principal = table.Column<bool>(type: "boolean", nullable: false, defaultValue: false),
                    ativo = table.Column<bool>(type: "boolean", nullable: false, defaultValue: true),
                    data_criacao = table.Column<DateTime>(type: "timestamp without time zone", nullable: false),
                    data_alteracao = table.Column<DateTime>(type: "timestamp without time zone", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_enderecos", x => x.id);
                    table.ForeignKey(
                        name: "FK_enderecos_usuarios_id_usuario",
                        column: x => x.id_usuario,
                        principalTable: "usuarios",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "logins_externos",
                columns: table => new
                {
                    id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    id_usuario = table.Column<int>(type: "integer", nullable: false),
                    provedor = table.Column<string>(type: "character varying(30)", maxLength: 30, nullable: false),
                    subject_id = table.Column<string>(type: "character varying(255)", maxLength: 255, nullable: false),
                    email_no_provedor = table.Column<string>(type: "character varying(255)", maxLength: 255, nullable: false),
                    data_vinculo = table.Column<DateTime>(type: "timestamp without time zone", nullable: false),
                    ultimo_uso_em = table.Column<DateTime>(type: "timestamp without time zone", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_logins_externos", x => x.id);
                    table.ForeignKey(
                        name: "FK_logins_externos_usuarios_id_usuario",
                        column: x => x.id_usuario,
                        principalTable: "usuarios",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "refresh_tokens",
                columns: table => new
                {
                    id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    id_usuario = table.Column<int>(type: "integer", nullable: false),
                    token_hash = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: false),
                    expira_em = table.Column<DateTime>(type: "timestamp without time zone", nullable: false),
                    criado_em = table.Column<DateTime>(type: "timestamp without time zone", nullable: false),
                    revogado_em = table.Column<DateTime>(type: "timestamp without time zone", nullable: true),
                    substituido_por_hash = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: true),
                    id_familia = table.Column<Guid>(type: "uuid", nullable: false),
                    ip_criacao = table.Column<string>(type: "character varying(45)", maxLength: 45, nullable: true),
                    user_agent = table.Column<string>(type: "character varying(400)", maxLength: 400, nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_refresh_tokens", x => x.id);
                    table.ForeignKey(
                        name: "FK_refresh_tokens_usuarios_id_usuario",
                        column: x => x.id_usuario,
                        principalTable: "usuarios",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "usuarios_roles",
                columns: table => new
                {
                    id_usuario = table.Column<int>(type: "integer", nullable: false),
                    id_role = table.Column<int>(type: "integer", nullable: false),
                    concedida_em = table.Column<DateTime>(type: "timestamp without time zone", nullable: false),
                    concedida_por = table.Column<int>(type: "integer", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_usuarios_roles", x => new { x.id_usuario, x.id_role });
                    table.ForeignKey(
                        name: "FK_usuarios_roles_roles_id_role",
                        column: x => x.id_role,
                        principalTable: "roles",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_usuarios_roles_usuarios_concedida_por",
                        column: x => x.concedida_por,
                        principalTable: "usuarios",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_usuarios_roles_usuarios_id_usuario",
                        column: x => x.id_usuario,
                        principalTable: "usuarios",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "produtos",
                columns: table => new
                {
                    id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    nome = table.Column<string>(type: "character varying(180)", maxLength: 180, nullable: false),
                    slug = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                    sku_base = table.Column<string>(type: "character varying(60)", maxLength: 60, nullable: false),
                    descricao = table.Column<string>(type: "text", nullable: true),
                    id_categoria = table.Column<int>(type: "integer", nullable: false),
                    genero = table.Column<int>(type: "integer", nullable: false),
                    preco_base_centavos = table.Column<int>(type: "integer", nullable: false),
                    preco_comparativo_centavos = table.Column<int>(type: "integer", nullable: true),
                    composicao_tecido = table.Column<string>(type: "character varying(400)", maxLength: 400, nullable: true),
                    instrucoes_lavagem = table.Column<string>(type: "text", nullable: true),
                    modelagem = table.Column<int>(type: "integer", nullable: true),
                    id_tabela_medidas = table.Column<int>(type: "integer", nullable: true),
                    destaque = table.Column<bool>(type: "boolean", nullable: false, defaultValue: false),
                    ativo = table.Column<bool>(type: "boolean", nullable: false, defaultValue: true),
                    meta_title = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: true),
                    meta_description = table.Column<string>(type: "character varying(400)", maxLength: 400, nullable: true),
                    nota_media = table.Column<decimal>(type: "numeric(2,1)", precision: 2, scale: 1, nullable: true),
                    total_avaliacoes = table.Column<int>(type: "integer", nullable: false, defaultValue: 0),
                    data_criacao = table.Column<DateTime>(type: "timestamp without time zone", nullable: false),
                    data_alteracao = table.Column<DateTime>(type: "timestamp without time zone", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_produtos", x => x.id);
                    table.ForeignKey(
                        name: "FK_produtos_categorias_id_categoria",
                        column: x => x.id_categoria,
                        principalTable: "categorias",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_produtos_tabelas_medidas_id_tabela_medidas",
                        column: x => x.id_tabela_medidas,
                        principalTable: "tabelas_medidas",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "cupons",
                columns: table => new
                {
                    id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    codigo = table.Column<string>(type: "character varying(40)", maxLength: 40, nullable: false),
                    descricao = table.Column<string>(type: "character varying(255)", maxLength: 255, nullable: true),
                    tipo = table.Column<int>(type: "integer", nullable: false),
                    valor = table.Column<int>(type: "integer", nullable: false),
                    valor_minimo_pedido_centavos = table.Column<int>(type: "integer", nullable: true),
                    desconto_maximo_centavos = table.Column<int>(type: "integer", nullable: true),
                    uso_maximo_total = table.Column<int>(type: "integer", nullable: true),
                    uso_maximo_por_usuario = table.Column<int>(type: "integer", nullable: false, defaultValue: 1),
                    usos_atuais = table.Column<int>(type: "integer", nullable: false, defaultValue: 0),
                    vigencia_inicio = table.Column<DateTime>(type: "timestamp without time zone", nullable: false),
                    vigencia_fim = table.Column<DateTime>(type: "timestamp without time zone", nullable: true),
                    primeira_compra_apenas = table.Column<bool>(type: "boolean", nullable: false, defaultValue: false),
                    id_categoria_restrita = table.Column<int>(type: "integer", nullable: true),
                    id_colecao_restrita = table.Column<int>(type: "integer", nullable: true),
                    ativo = table.Column<bool>(type: "boolean", nullable: false, defaultValue: true),
                    data_criacao = table.Column<DateTime>(type: "timestamp without time zone", nullable: false),
                    data_alteracao = table.Column<DateTime>(type: "timestamp without time zone", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_cupons", x => x.id);
                    table.ForeignKey(
                        name: "FK_cupons_categorias_id_categoria_restrita",
                        column: x => x.id_categoria_restrita,
                        principalTable: "categorias",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_cupons_colecoes_id_colecao_restrita",
                        column: x => x.id_colecao_restrita,
                        principalTable: "colecoes",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "logs_produtos",
                columns: table => new
                {
                    id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    id_produto = table.Column<int>(type: "integer", nullable: false),
                    ativo_antigo = table.Column<bool>(type: "boolean", nullable: true),
                    ativo_novo = table.Column<bool>(type: "boolean", nullable: false),
                    id_usuario = table.Column<int>(type: "integer", nullable: true),
                    data_alteracao = table.Column<DateTime>(type: "timestamp without time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_logs_produtos", x => x.id);
                    table.ForeignKey(
                        name: "FK_logs_produtos_produtos_id_produto",
                        column: x => x.id_produto,
                        principalTable: "produtos",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_logs_produtos_usuarios_id_usuario",
                        column: x => x.id_usuario,
                        principalTable: "usuarios",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "midias_produtos",
                columns: table => new
                {
                    id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    id_produto = table.Column<int>(type: "integer", nullable: false),
                    id_midia = table.Column<int>(type: "integer", nullable: false),
                    id_cor = table.Column<int>(type: "integer", nullable: true),
                    ordem = table.Column<int>(type: "integer", nullable: false, defaultValue: 0),
                    eh_capa = table.Column<bool>(type: "boolean", nullable: false, defaultValue: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_midias_produtos", x => x.id);
                    table.ForeignKey(
                        name: "FK_midias_produtos_cores_id_cor",
                        column: x => x.id_cor,
                        principalTable: "cores",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_midias_produtos_midias_id_midia",
                        column: x => x.id_midia,
                        principalTable: "midias",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_midias_produtos_produtos_id_produto",
                        column: x => x.id_produto,
                        principalTable: "produtos",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "produto_variacoes",
                columns: table => new
                {
                    id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    id_produto = table.Column<int>(type: "integer", nullable: false),
                    sku = table.Column<string>(type: "character varying(60)", maxLength: 60, nullable: false),
                    id_tamanho = table.Column<int>(type: "integer", nullable: false),
                    id_cor = table.Column<int>(type: "integer", nullable: false),
                    preco_centavos = table.Column<int>(type: "integer", nullable: true),
                    codigo_barras = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: true),
                    peso_gramas = table.Column<int>(type: "integer", nullable: false),
                    altura_cm = table.Column<decimal>(type: "numeric(8,2)", precision: 8, scale: 2, nullable: false),
                    largura_cm = table.Column<decimal>(type: "numeric(8,2)", precision: 8, scale: 2, nullable: false),
                    comprimento_cm = table.Column<decimal>(type: "numeric(8,2)", precision: 8, scale: 2, nullable: false),
                    ativo = table.Column<bool>(type: "boolean", nullable: false, defaultValue: true),
                    data_criacao = table.Column<DateTime>(type: "timestamp without time zone", nullable: false),
                    data_alteracao = table.Column<DateTime>(type: "timestamp without time zone", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_produto_variacoes", x => x.id);
                    table.CheckConstraint("ck_produto_variacoes_dimensoes", "peso_gramas > 0 AND altura_cm > 0 AND largura_cm > 0 AND comprimento_cm > 0");
                    table.ForeignKey(
                        name: "FK_produto_variacoes_cores_id_cor",
                        column: x => x.id_cor,
                        principalTable: "cores",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_produto_variacoes_produtos_id_produto",
                        column: x => x.id_produto,
                        principalTable: "produtos",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_produto_variacoes_tamanhos_id_tamanho",
                        column: x => x.id_tamanho,
                        principalTable: "tamanhos",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "produtos_colecoes",
                columns: table => new
                {
                    id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    id_produto = table.Column<int>(type: "integer", nullable: false),
                    id_colecao = table.Column<int>(type: "integer", nullable: false),
                    ordem = table.Column<int>(type: "integer", nullable: false, defaultValue: 0)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_produtos_colecoes", x => x.id);
                    table.ForeignKey(
                        name: "FK_produtos_colecoes_colecoes_id_colecao",
                        column: x => x.id_colecao,
                        principalTable: "colecoes",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_produtos_colecoes_produtos_id_produto",
                        column: x => x.id_produto,
                        principalTable: "produtos",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "carrinhos",
                columns: table => new
                {
                    id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    uuid = table.Column<string>(type: "character varying(36)", maxLength: 36, nullable: false),
                    id_usuario = table.Column<int>(type: "integer", nullable: true),
                    chave_sessao = table.Column<string>(type: "character varying(120)", maxLength: 120, nullable: true),
                    status = table.Column<int>(type: "integer", nullable: false),
                    id_cupom = table.Column<int>(type: "integer", nullable: true),
                    data_criacao = table.Column<DateTime>(type: "timestamp without time zone", nullable: false),
                    data_alteracao = table.Column<DateTime>(type: "timestamp without time zone", nullable: true),
                    expira_em = table.Column<DateTime>(type: "timestamp without time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_carrinhos", x => x.id);
                    table.ForeignKey(
                        name: "FK_carrinhos_cupons_id_cupom",
                        column: x => x.id_cupom,
                        principalTable: "cupons",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_carrinhos_usuarios_id_usuario",
                        column: x => x.id_usuario,
                        principalTable: "usuarios",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "pedidos",
                columns: table => new
                {
                    id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    numero = table.Column<string>(type: "character varying(30)", maxLength: 30, nullable: false),
                    uuid = table.Column<string>(type: "character varying(36)", maxLength: 36, nullable: false),
                    id_usuario = table.Column<int>(type: "integer", nullable: false),
                    status = table.Column<int>(type: "integer", nullable: false),
                    subtotal_centavos = table.Column<int>(type: "integer", nullable: false),
                    desconto_cupom_centavos = table.Column<int>(type: "integer", nullable: false),
                    frete_centavos = table.Column<int>(type: "integer", nullable: false),
                    total_centavos = table.Column<int>(type: "integer", nullable: false),
                    id_cupom = table.Column<int>(type: "integer", nullable: true),
                    codigo_cupom_snapshot = table.Column<string>(type: "character varying(40)", maxLength: 40, nullable: true),
                    id_servico_frete = table.Column<int>(type: "integer", nullable: true),
                    transportadora_frete = table.Column<string>(type: "character varying(120)", maxLength: 120, nullable: true),
                    servico_frete = table.Column<string>(type: "character varying(120)", maxLength: 120, nullable: true),
                    prazo_frete_dias = table.Column<int>(type: "integer", nullable: true),
                    entrega_destinatario = table.Column<string>(type: "character varying(180)", maxLength: 180, nullable: false),
                    entrega_documento_destinatario = table.Column<string>(type: "character varying(14)", maxLength: 14, nullable: false),
                    entrega_telefone_contato = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false),
                    entrega_cep = table.Column<string>(type: "character varying(8)", maxLength: 8, nullable: false),
                    entrega_logradouro = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                    entrega_numero = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false),
                    entrega_complemento = table.Column<string>(type: "character varying(120)", maxLength: 120, nullable: true),
                    entrega_bairro = table.Column<string>(type: "character varying(120)", maxLength: 120, nullable: false),
                    entrega_cidade = table.Column<string>(type: "character varying(120)", maxLength: 120, nullable: false),
                    entrega_uf = table.Column<string>(type: "char(2)", nullable: false),
                    entrega_pais = table.Column<string>(type: "character varying(2)", maxLength: 2, nullable: false, defaultValue: "BR"),
                    observacao_cliente = table.Column<string>(type: "text", nullable: true),
                    peso_total_gramas = table.Column<int>(type: "integer", nullable: false),
                    data_criacao = table.Column<DateTime>(type: "timestamp without time zone", nullable: false),
                    data_pagamento = table.Column<DateTime>(type: "timestamp without time zone", nullable: true),
                    data_envio = table.Column<DateTime>(type: "timestamp without time zone", nullable: true),
                    data_entrega = table.Column<DateTime>(type: "timestamp without time zone", nullable: true),
                    data_cancelamento = table.Column<DateTime>(type: "timestamp without time zone", nullable: true),
                    motivo_cancelamento = table.Column<string>(type: "character varying(400)", maxLength: 400, nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_pedidos", x => x.id);
                    table.ForeignKey(
                        name: "FK_pedidos_cupons_id_cupom",
                        column: x => x.id_cupom,
                        principalTable: "cupons",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_pedidos_usuarios_id_usuario",
                        column: x => x.id_usuario,
                        principalTable: "usuarios",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "estoques_variacoes",
                columns: table => new
                {
                    id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    id_variacao = table.Column<int>(type: "integer", nullable: false),
                    quantidade = table.Column<int>(type: "integer", nullable: false, defaultValue: 0),
                    quantidade_reservada = table.Column<int>(type: "integer", nullable: false, defaultValue: 0),
                    quantidade_minima = table.Column<int>(type: "integer", nullable: false, defaultValue: 0),
                    localizacao = table.Column<string>(type: "character varying(120)", maxLength: 120, nullable: true),
                    data_ultima_movimentacao = table.Column<DateTime>(type: "timestamp without time zone", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_estoques_variacoes", x => x.id);
                    table.CheckConstraint("ck_estoques_variacoes_quantidades", "quantidade >= 0 AND quantidade_reservada >= 0 AND quantidade_reservada <= quantidade");
                    table.ForeignKey(
                        name: "FK_estoques_variacoes_produto_variacoes_id_variacao",
                        column: x => x.id_variacao,
                        principalTable: "produto_variacoes",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "lista_desejo_itens",
                columns: table => new
                {
                    id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    id_usuario = table.Column<int>(type: "integer", nullable: false),
                    id_produto = table.Column<int>(type: "integer", nullable: false),
                    id_variacao = table.Column<int>(type: "integer", nullable: true),
                    data_criacao = table.Column<DateTime>(type: "timestamp without time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_lista_desejo_itens", x => x.id);
                    table.ForeignKey(
                        name: "FK_lista_desejo_itens_produto_variacoes_id_variacao",
                        column: x => x.id_variacao,
                        principalTable: "produto_variacoes",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_lista_desejo_itens_produtos_id_produto",
                        column: x => x.id_produto,
                        principalTable: "produtos",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_lista_desejo_itens_usuarios_id_usuario",
                        column: x => x.id_usuario,
                        principalTable: "usuarios",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "carrinho_itens",
                columns: table => new
                {
                    id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    id_carrinho = table.Column<int>(type: "integer", nullable: false),
                    id_variacao = table.Column<int>(type: "integer", nullable: false),
                    quantidade = table.Column<int>(type: "integer", nullable: false),
                    preco_unitario_snapshot_centavos = table.Column<int>(type: "integer", nullable: false),
                    data_adicao = table.Column<DateTime>(type: "timestamp without time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_carrinho_itens", x => x.id);
                    table.ForeignKey(
                        name: "FK_carrinho_itens_carrinhos_id_carrinho",
                        column: x => x.id_carrinho,
                        principalTable: "carrinhos",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_carrinho_itens_produto_variacoes_id_variacao",
                        column: x => x.id_variacao,
                        principalTable: "produto_variacoes",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "cupons_usos",
                columns: table => new
                {
                    id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    id_cupom = table.Column<int>(type: "integer", nullable: false),
                    id_usuario = table.Column<int>(type: "integer", nullable: false),
                    id_pedido = table.Column<int>(type: "integer", nullable: false),
                    valor_descontado_centavos = table.Column<int>(type: "integer", nullable: false),
                    data_uso = table.Column<DateTime>(type: "timestamp without time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_cupons_usos", x => x.id);
                    table.ForeignKey(
                        name: "FK_cupons_usos_cupons_id_cupom",
                        column: x => x.id_cupom,
                        principalTable: "cupons",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_cupons_usos_pedidos_id_pedido",
                        column: x => x.id_pedido,
                        principalTable: "pedidos",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_cupons_usos_usuarios_id_usuario",
                        column: x => x.id_usuario,
                        principalTable: "usuarios",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "envios",
                columns: table => new
                {
                    id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    id_pedido = table.Column<int>(type: "integer", nullable: false),
                    me_order_id = table.Column<string>(type: "character varying(80)", maxLength: 80, nullable: true),
                    id_servico = table.Column<int>(type: "integer", nullable: false),
                    nome_servico = table.Column<string>(type: "character varying(120)", maxLength: 120, nullable: true),
                    nome_transportadora = table.Column<string>(type: "character varying(120)", maxLength: 120, nullable: true),
                    valor_cotado_centavos = table.Column<int>(type: "integer", nullable: false),
                    valor_comprado_centavos = table.Column<int>(type: "integer", nullable: true),
                    status = table.Column<int>(type: "integer", nullable: false),
                    codigo_rastreio = table.Column<string>(type: "character varying(60)", maxLength: 60, nullable: true),
                    url_etiqueta = table.Column<string>(type: "text", nullable: true),
                    chave_nfe = table.Column<string>(type: "character varying(44)", maxLength: 44, nullable: true),
                    tentativas = table.Column<int>(type: "integer", nullable: false, defaultValue: 0),
                    ultimo_erro = table.Column<string>(type: "character varying(2000)", maxLength: 2000, nullable: true),
                    proxima_tentativa_em = table.Column<DateTime>(type: "timestamp without time zone", nullable: true),
                    raw_ultima_resposta = table.Column<string>(type: "jsonb", nullable: true),
                    data_criacao = table.Column<DateTime>(type: "timestamp without time zone", nullable: false),
                    data_alteracao = table.Column<DateTime>(type: "timestamp without time zone", nullable: true),
                    xmin = table.Column<uint>(type: "xid", rowVersion: true, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_envios", x => x.id);
                    table.ForeignKey(
                        name: "FK_envios_pedidos_id_pedido",
                        column: x => x.id_pedido,
                        principalTable: "pedidos",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "movimentacoes_estoque",
                columns: table => new
                {
                    id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    id_variacao = table.Column<int>(type: "integer", nullable: false),
                    id_movimento = table.Column<int>(type: "integer", nullable: false),
                    quantidade = table.Column<int>(type: "integer", nullable: false),
                    quantidade_antes = table.Column<int>(type: "integer", nullable: false),
                    quantidade_depois = table.Column<int>(type: "integer", nullable: false),
                    id_pedido = table.Column<int>(type: "integer", nullable: true),
                    id_usuario = table.Column<int>(type: "integer", nullable: true),
                    observacao = table.Column<string>(type: "character varying(400)", maxLength: 400, nullable: true),
                    data_movimentacao = table.Column<DateTime>(type: "timestamp without time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_movimentacoes_estoque", x => x.id);
                    table.ForeignKey(
                        name: "FK_movimentacoes_estoque_movimentos_estoque_id_movimento",
                        column: x => x.id_movimento,
                        principalTable: "movimentos_estoque",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_movimentacoes_estoque_pedidos_id_pedido",
                        column: x => x.id_pedido,
                        principalTable: "pedidos",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_movimentacoes_estoque_produto_variacoes_id_variacao",
                        column: x => x.id_variacao,
                        principalTable: "produto_variacoes",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_movimentacoes_estoque_usuarios_id_usuario",
                        column: x => x.id_usuario,
                        principalTable: "usuarios",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "pagamentos",
                columns: table => new
                {
                    id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    id_pedido = table.Column<int>(type: "integer", nullable: false),
                    provedor = table.Column<string>(type: "character varying(40)", maxLength: 40, nullable: false),
                    metodo = table.Column<string>(type: "character varying(40)", maxLength: 40, nullable: true),
                    status = table.Column<int>(type: "integer", nullable: false),
                    valor_centavos = table.Column<int>(type: "integer", nullable: false),
                    parcelas = table.Column<int>(type: "integer", nullable: true),
                    provider_order_id = table.Column<string>(type: "character varying(120)", maxLength: 120, nullable: true),
                    provider_charge_id = table.Column<string>(type: "character varying(120)", maxLength: 120, nullable: true),
                    payment_url = table.Column<string>(type: "text", nullable: true),
                    qr_code_pix = table.Column<string>(type: "text", nullable: true),
                    linha_digitavel = table.Column<string>(type: "character varying(120)", maxLength: 120, nullable: true),
                    expira_em = table.Column<DateTime>(type: "timestamp without time zone", nullable: true),
                    raw_ultima_resposta = table.Column<string>(type: "jsonb", nullable: true),
                    data_criacao = table.Column<DateTime>(type: "timestamp without time zone", nullable: false),
                    data_confirmacao = table.Column<DateTime>(type: "timestamp without time zone", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_pagamentos", x => x.id);
                    table.ForeignKey(
                        name: "FK_pagamentos_pedidos_id_pedido",
                        column: x => x.id_pedido,
                        principalTable: "pedidos",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "pedido_historicos",
                columns: table => new
                {
                    id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    id_pedido = table.Column<int>(type: "integer", nullable: false),
                    status_anterior = table.Column<int>(type: "integer", nullable: true),
                    status_novo = table.Column<int>(type: "integer", nullable: false),
                    id_usuario = table.Column<int>(type: "integer", nullable: true),
                    observacao = table.Column<string>(type: "character varying(400)", maxLength: 400, nullable: true),
                    data_alteracao = table.Column<DateTime>(type: "timestamp without time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_pedido_historicos", x => x.id);
                    table.ForeignKey(
                        name: "FK_pedido_historicos_pedidos_id_pedido",
                        column: x => x.id_pedido,
                        principalTable: "pedidos",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_pedido_historicos_usuarios_id_usuario",
                        column: x => x.id_usuario,
                        principalTable: "usuarios",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "pedido_itens",
                columns: table => new
                {
                    id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    id_pedido = table.Column<int>(type: "integer", nullable: false),
                    id_variacao = table.Column<int>(type: "integer", nullable: false),
                    id_produto = table.Column<int>(type: "integer", nullable: false),
                    sku_snapshot = table.Column<string>(type: "character varying(60)", maxLength: 60, nullable: false),
                    nome_produto_snapshot = table.Column<string>(type: "character varying(180)", maxLength: 180, nullable: false),
                    tamanho_snapshot = table.Column<string>(type: "character varying(10)", maxLength: 10, nullable: false),
                    cor_snapshot = table.Column<string>(type: "character varying(80)", maxLength: 80, nullable: false),
                    imagem_url_snapshot = table.Column<string>(type: "text", nullable: true),
                    quantidade = table.Column<int>(type: "integer", nullable: false),
                    preco_unitario_centavos = table.Column<int>(type: "integer", nullable: false),
                    desconto_unitario_centavos = table.Column<int>(type: "integer", nullable: false),
                    peso_gramas_snapshot = table.Column<int>(type: "integer", nullable: false),
                    total_linha_centavos = table.Column<int>(type: "integer", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_pedido_itens", x => x.id);
                    table.ForeignKey(
                        name: "FK_pedido_itens_pedidos_id_pedido",
                        column: x => x.id_pedido,
                        principalTable: "pedidos",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_pedido_itens_produto_variacoes_id_variacao",
                        column: x => x.id_variacao,
                        principalTable: "produto_variacoes",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_pedido_itens_produtos_id_produto",
                        column: x => x.id_produto,
                        principalTable: "produtos",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "envios_eventos",
                columns: table => new
                {
                    id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    id_envio = table.Column<int>(type: "integer", nullable: false),
                    status = table.Column<int>(type: "integer", nullable: false),
                    descricao = table.Column<string>(type: "character varying(400)", maxLength: 400, nullable: true),
                    local = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: true),
                    ocorrido_em = table.Column<DateTime>(type: "timestamp without time zone", nullable: false),
                    registrado_em = table.Column<DateTime>(type: "timestamp without time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_envios_eventos", x => x.id);
                    table.ForeignKey(
                        name: "FK_envios_eventos_envios_id_envio",
                        column: x => x.id_envio,
                        principalTable: "envios",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "pagamentos_eventos",
                columns: table => new
                {
                    id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    id_pagamento = table.Column<int>(type: "integer", nullable: true),
                    provider_event_id = table.Column<string>(type: "character varying(160)", maxLength: 160, nullable: false),
                    tipo = table.Column<string>(type: "character varying(80)", maxLength: 80, nullable: false),
                    payload = table.Column<string>(type: "jsonb", nullable: false),
                    recebido_em = table.Column<DateTime>(type: "timestamp without time zone", nullable: false),
                    processado_em = table.Column<DateTime>(type: "timestamp without time zone", nullable: true),
                    erro = table.Column<string>(type: "text", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_pagamentos_eventos", x => x.id);
                    table.ForeignKey(
                        name: "FK_pagamentos_eventos_pagamentos_id_pagamento",
                        column: x => x.id_pagamento,
                        principalTable: "pagamentos",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "avaliacoes",
                columns: table => new
                {
                    id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    id_produto = table.Column<int>(type: "integer", nullable: false),
                    id_usuario = table.Column<int>(type: "integer", nullable: false),
                    id_pedido_item = table.Column<int>(type: "integer", nullable: true),
                    nota = table.Column<int>(type: "integer", nullable: false),
                    titulo = table.Column<string>(type: "character varying(160)", maxLength: 160, nullable: true),
                    comentario = table.Column<string>(type: "text", nullable: true),
                    tamanho_comprado = table.Column<string>(type: "character varying(10)", maxLength: 10, nullable: true),
                    altura_cliente_cm = table.Column<int>(type: "integer", nullable: true),
                    peso_cliente_kg = table.Column<decimal>(type: "numeric(5,2)", precision: 5, scale: 2, nullable: true),
                    caimento = table.Column<int>(type: "integer", nullable: true),
                    recomenda = table.Column<bool>(type: "boolean", nullable: true),
                    status = table.Column<int>(type: "integer", nullable: false),
                    motivo_rejeicao = table.Column<string>(type: "character varying(400)", maxLength: 400, nullable: true),
                    moderada_por = table.Column<int>(type: "integer", nullable: true),
                    moderada_em = table.Column<DateTime>(type: "timestamp without time zone", nullable: true),
                    data_criacao = table.Column<DateTime>(type: "timestamp without time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_avaliacoes", x => x.id);
                    table.CheckConstraint("ck_avaliacoes_nota", "nota BETWEEN 1 AND 5");
                    table.ForeignKey(
                        name: "FK_avaliacoes_pedido_itens_id_pedido_item",
                        column: x => x.id_pedido_item,
                        principalTable: "pedido_itens",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_avaliacoes_produtos_id_produto",
                        column: x => x.id_produto,
                        principalTable: "produtos",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_avaliacoes_usuarios_id_usuario",
                        column: x => x.id_usuario,
                        principalTable: "usuarios",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_avaliacoes_usuarios_moderada_por",
                        column: x => x.moderada_por,
                        principalTable: "usuarios",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "avaliacoes_midias",
                columns: table => new
                {
                    id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    id_avaliacao = table.Column<int>(type: "integer", nullable: false),
                    id_midia = table.Column<int>(type: "integer", nullable: false),
                    ordem = table.Column<int>(type: "integer", nullable: false, defaultValue: 0)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_avaliacoes_midias", x => x.id);
                    table.ForeignKey(
                        name: "FK_avaliacoes_midias_avaliacoes_id_avaliacao",
                        column: x => x.id_avaliacao,
                        principalTable: "avaliacoes",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_avaliacoes_midias_midias_id_midia",
                        column: x => x.id_midia,
                        principalTable: "midias",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateIndex(
                name: "ux_app_secrets_config_key",
                table: "app_secrets",
                column: "config_key",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_avaliacoes_id_pedido_item",
                table: "avaliacoes",
                column: "id_pedido_item");

            migrationBuilder.CreateIndex(
                name: "IX_avaliacoes_id_usuario",
                table: "avaliacoes",
                column: "id_usuario");

            migrationBuilder.CreateIndex(
                name: "IX_avaliacoes_moderada_por",
                table: "avaliacoes",
                column: "moderada_por");

            migrationBuilder.CreateIndex(
                name: "ux_avaliacoes_produto_usuario",
                table: "avaliacoes",
                columns: new[] { "id_produto", "id_usuario" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_avaliacoes_midias_id_avaliacao",
                table: "avaliacoes_midias",
                column: "id_avaliacao");

            migrationBuilder.CreateIndex(
                name: "IX_avaliacoes_midias_id_midia",
                table: "avaliacoes_midias",
                column: "id_midia");

            migrationBuilder.CreateIndex(
                name: "IX_carrinho_itens_id_variacao",
                table: "carrinho_itens",
                column: "id_variacao");

            migrationBuilder.CreateIndex(
                name: "ux_carrinho_itens_carrinho_variacao",
                table: "carrinho_itens",
                columns: new[] { "id_carrinho", "id_variacao" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_carrinhos_id_cupom",
                table: "carrinhos",
                column: "id_cupom");

            migrationBuilder.CreateIndex(
                name: "ux_carrinhos_chave_sessao_aberto",
                table: "carrinhos",
                column: "chave_sessao",
                unique: true,
                filter: "status = 1");

            migrationBuilder.CreateIndex(
                name: "ux_carrinhos_usuario_aberto",
                table: "carrinhos",
                column: "id_usuario",
                unique: true,
                filter: "status = 1");

            migrationBuilder.CreateIndex(
                name: "ux_carrinhos_uuid",
                table: "carrinhos",
                column: "uuid",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_categorias_id_categoria_pai",
                table: "categorias",
                column: "id_categoria_pai");

            migrationBuilder.CreateIndex(
                name: "IX_categorias_id_midia_capa",
                table: "categorias",
                column: "id_midia_capa");

            migrationBuilder.CreateIndex(
                name: "ux_categorias_slug",
                table: "categorias",
                column: "slug",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_colecoes_id_midia_banner",
                table: "colecoes",
                column: "id_midia_banner");

            migrationBuilder.CreateIndex(
                name: "IX_colecoes_id_midia_capa",
                table: "colecoes",
                column: "id_midia_capa");

            migrationBuilder.CreateIndex(
                name: "ux_colecoes_slug",
                table: "colecoes",
                column: "slug",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_cores_id_midia_swatch",
                table: "cores",
                column: "id_midia_swatch");

            migrationBuilder.CreateIndex(
                name: "ux_cores_slug",
                table: "cores",
                column: "slug",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_cupons_id_categoria_restrita",
                table: "cupons",
                column: "id_categoria_restrita");

            migrationBuilder.CreateIndex(
                name: "IX_cupons_id_colecao_restrita",
                table: "cupons",
                column: "id_colecao_restrita");

            migrationBuilder.CreateIndex(
                name: "ux_cupons_codigo",
                table: "cupons",
                column: "codigo",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_cupons_usos_id_pedido",
                table: "cupons_usos",
                column: "id_pedido");

            migrationBuilder.CreateIndex(
                name: "IX_cupons_usos_id_usuario",
                table: "cupons_usos",
                column: "id_usuario");

            migrationBuilder.CreateIndex(
                name: "ux_cupons_usos_cupom_pedido",
                table: "cupons_usos",
                columns: new[] { "id_cupom", "id_pedido" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_enderecos_id_usuario",
                table: "enderecos",
                column: "id_usuario");

            migrationBuilder.CreateIndex(
                name: "ix_envios_me_order_id",
                table: "envios",
                column: "me_order_id");

            migrationBuilder.CreateIndex(
                name: "ix_envios_status_proxima_tentativa",
                table: "envios",
                columns: new[] { "status", "proxima_tentativa_em" });

            migrationBuilder.CreateIndex(
                name: "ux_envios_pedido",
                table: "envios",
                column: "id_pedido",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_envios_eventos_id_envio",
                table: "envios_eventos",
                column: "id_envio");

            migrationBuilder.CreateIndex(
                name: "ux_estoques_variacoes_variacao",
                table: "estoques_variacoes",
                column: "id_variacao",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_lista_desejo_itens_id_produto",
                table: "lista_desejo_itens",
                column: "id_produto");

            migrationBuilder.CreateIndex(
                name: "IX_lista_desejo_itens_id_variacao",
                table: "lista_desejo_itens",
                column: "id_variacao");

            migrationBuilder.CreateIndex(
                name: "ux_lista_desejo_itens_usuario_produto",
                table: "lista_desejo_itens",
                columns: new[] { "id_usuario", "id_produto" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_logins_externos_id_usuario",
                table: "logins_externos",
                column: "id_usuario");

            migrationBuilder.CreateIndex(
                name: "ux_logins_externos_provedor_subject",
                table: "logins_externos",
                columns: new[] { "provedor", "subject_id" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_logs_produtos_id_produto",
                table: "logs_produtos",
                column: "id_produto");

            migrationBuilder.CreateIndex(
                name: "IX_logs_produtos_id_usuario",
                table: "logs_produtos",
                column: "id_usuario");

            migrationBuilder.CreateIndex(
                name: "IX_midias_produtos_id_cor",
                table: "midias_produtos",
                column: "id_cor");

            migrationBuilder.CreateIndex(
                name: "IX_midias_produtos_id_midia",
                table: "midias_produtos",
                column: "id_midia");

            migrationBuilder.CreateIndex(
                name: "ux_midias_produtos_produto_midia",
                table: "midias_produtos",
                columns: new[] { "id_produto", "id_midia" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_movimentacoes_estoque_id_movimento",
                table: "movimentacoes_estoque",
                column: "id_movimento");

            migrationBuilder.CreateIndex(
                name: "IX_movimentacoes_estoque_id_pedido",
                table: "movimentacoes_estoque",
                column: "id_pedido");

            migrationBuilder.CreateIndex(
                name: "IX_movimentacoes_estoque_id_usuario",
                table: "movimentacoes_estoque",
                column: "id_usuario");

            migrationBuilder.CreateIndex(
                name: "IX_movimentacoes_estoque_id_variacao",
                table: "movimentacoes_estoque",
                column: "id_variacao");

            migrationBuilder.CreateIndex(
                name: "ux_movimentos_estoque_nome",
                table: "movimentos_estoque",
                column: "nome",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "ix_pagamentos_provider_charge_id",
                table: "pagamentos",
                column: "provider_charge_id");

            migrationBuilder.CreateIndex(
                name: "ix_pagamentos_provider_order_id",
                table: "pagamentos",
                column: "provider_order_id");

            migrationBuilder.CreateIndex(
                name: "ux_pagamentos_pedido",
                table: "pagamentos",
                column: "id_pedido",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_pagamentos_eventos_id_pagamento",
                table: "pagamentos_eventos",
                column: "id_pagamento");

            migrationBuilder.CreateIndex(
                name: "ux_pagamentos_eventos_provider_event_id",
                table: "pagamentos_eventos",
                column: "provider_event_id",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_pedido_historicos_id_pedido",
                table: "pedido_historicos",
                column: "id_pedido");

            migrationBuilder.CreateIndex(
                name: "IX_pedido_historicos_id_usuario",
                table: "pedido_historicos",
                column: "id_usuario");

            migrationBuilder.CreateIndex(
                name: "IX_pedido_itens_id_pedido",
                table: "pedido_itens",
                column: "id_pedido");

            migrationBuilder.CreateIndex(
                name: "IX_pedido_itens_id_produto",
                table: "pedido_itens",
                column: "id_produto");

            migrationBuilder.CreateIndex(
                name: "IX_pedido_itens_id_variacao",
                table: "pedido_itens",
                column: "id_variacao");

            migrationBuilder.CreateIndex(
                name: "IX_pedidos_id_cupom",
                table: "pedidos",
                column: "id_cupom");

            migrationBuilder.CreateIndex(
                name: "ix_pedidos_usuario_data_criacao",
                table: "pedidos",
                columns: new[] { "id_usuario", "data_criacao" });

            migrationBuilder.CreateIndex(
                name: "ux_pedidos_numero",
                table: "pedidos",
                column: "numero",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "ux_pedidos_uuid",
                table: "pedidos",
                column: "uuid",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_produto_variacoes_id_cor",
                table: "produto_variacoes",
                column: "id_cor");

            migrationBuilder.CreateIndex(
                name: "IX_produto_variacoes_id_tamanho",
                table: "produto_variacoes",
                column: "id_tamanho");

            migrationBuilder.CreateIndex(
                name: "ux_produto_variacoes_produto_tamanho_cor",
                table: "produto_variacoes",
                columns: new[] { "id_produto", "id_tamanho", "id_cor" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "ux_produto_variacoes_sku",
                table: "produto_variacoes",
                column: "sku",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "ix_produtos_categoria_ativo",
                table: "produtos",
                columns: new[] { "id_categoria", "ativo" });

            migrationBuilder.CreateIndex(
                name: "IX_produtos_id_tabela_medidas",
                table: "produtos",
                column: "id_tabela_medidas");

            migrationBuilder.CreateIndex(
                name: "ux_produtos_sku_base",
                table: "produtos",
                column: "sku_base",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "ux_produtos_slug",
                table: "produtos",
                column: "slug",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_produtos_colecoes_id_colecao",
                table: "produtos_colecoes",
                column: "id_colecao");

            migrationBuilder.CreateIndex(
                name: "IX_produtos_colecoes_id_produto",
                table: "produtos_colecoes",
                column: "id_produto");

            migrationBuilder.CreateIndex(
                name: "IX_refresh_tokens_id_usuario",
                table: "refresh_tokens",
                column: "id_usuario");

            migrationBuilder.CreateIndex(
                name: "ux_refresh_tokens_token_hash",
                table: "refresh_tokens",
                column: "token_hash",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "ux_roles_nome",
                table: "roles",
                column: "nome",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_tabelas_medidas_linhas_id_tabela_medidas",
                table: "tabelas_medidas_linhas",
                column: "id_tabela_medidas");

            migrationBuilder.CreateIndex(
                name: "IX_tabelas_medidas_linhas_id_tamanho",
                table: "tabelas_medidas_linhas",
                column: "id_tamanho");

            migrationBuilder.CreateIndex(
                name: "ux_tamanhos_grade_codigo",
                table: "tamanhos",
                columns: new[] { "grade", "codigo" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "ux_usuarios_cpf",
                table: "usuarios",
                column: "cpf",
                unique: true,
                filter: "cpf IS NOT NULL");

            migrationBuilder.CreateIndex(
                name: "ux_usuarios_email",
                table: "usuarios",
                column: "email",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "ux_usuarios_uuid",
                table: "usuarios",
                column: "uuid",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_usuarios_roles_concedida_por",
                table: "usuarios_roles",
                column: "concedida_por");

            migrationBuilder.CreateIndex(
                name: "IX_usuarios_roles_id_role",
                table: "usuarios_roles",
                column: "id_role");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "app_secrets");

            migrationBuilder.DropTable(
                name: "avaliacoes_midias");

            migrationBuilder.DropTable(
                name: "carrinho_itens");

            migrationBuilder.DropTable(
                name: "configuracoes_loja");

            migrationBuilder.DropTable(
                name: "cupons_usos");

            migrationBuilder.DropTable(
                name: "enderecos");

            migrationBuilder.DropTable(
                name: "envios_eventos");

            migrationBuilder.DropTable(
                name: "estoques_variacoes");

            migrationBuilder.DropTable(
                name: "lista_desejo_itens");

            migrationBuilder.DropTable(
                name: "logins_externos");

            migrationBuilder.DropTable(
                name: "logs_produtos");

            migrationBuilder.DropTable(
                name: "midias_produtos");

            migrationBuilder.DropTable(
                name: "movimentacoes_estoque");

            migrationBuilder.DropTable(
                name: "pagamentos_eventos");

            migrationBuilder.DropTable(
                name: "pedido_historicos");

            migrationBuilder.DropTable(
                name: "produtos_colecoes");

            migrationBuilder.DropTable(
                name: "refresh_tokens");

            migrationBuilder.DropTable(
                name: "tabelas_medidas_linhas");

            migrationBuilder.DropTable(
                name: "usuarios_roles");

            migrationBuilder.DropTable(
                name: "avaliacoes");

            migrationBuilder.DropTable(
                name: "carrinhos");

            migrationBuilder.DropTable(
                name: "envios");

            migrationBuilder.DropTable(
                name: "movimentos_estoque");

            migrationBuilder.DropTable(
                name: "pagamentos");

            migrationBuilder.DropTable(
                name: "roles");

            migrationBuilder.DropTable(
                name: "pedido_itens");

            migrationBuilder.DropTable(
                name: "pedidos");

            migrationBuilder.DropTable(
                name: "produto_variacoes");

            migrationBuilder.DropTable(
                name: "cupons");

            migrationBuilder.DropTable(
                name: "usuarios");

            migrationBuilder.DropTable(
                name: "cores");

            migrationBuilder.DropTable(
                name: "produtos");

            migrationBuilder.DropTable(
                name: "tamanhos");

            migrationBuilder.DropTable(
                name: "colecoes");

            migrationBuilder.DropTable(
                name: "categorias");

            migrationBuilder.DropTable(
                name: "tabelas_medidas");

            migrationBuilder.DropTable(
                name: "midias");
        }
    }
}
