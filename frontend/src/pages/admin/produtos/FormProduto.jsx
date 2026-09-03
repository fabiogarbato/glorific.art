import { useEffect, useMemo, useState } from "react";
import { Link, useNavigate, useParams } from "react-router-dom";
import { FiArrowLeft, FiImage, FiGrid, FiSave, FiSliders } from "react-icons/fi";

import Badge from "@/components/ui/Badge.jsx";
import Botao from "@/components/ui/Botao.jsx";
import Campo from "@/components/ui/Campo.jsx";
import ConfirmModal from "@/components/ui/ConfirmModal.jsx";
import Skeleton from "@/components/ui/Skeleton.jsx";

import Aviso from "@/components/admin/Aviso.jsx";
import CabecalhoPagina from "@/components/admin/CabecalhoPagina.jsx";
import CampoDinheiro from "@/components/admin/CampoDinheiro.jsx";
import MatrizVariacoes from "@/components/admin/MatrizVariacoes.jsx";
import UploadImagens from "@/components/admin/UploadImagens.jsx";
import { CAMPO } from "@/components/admin/chaves.js";

import { useArvoreCategorias } from "@/hooks/admin/useCategoriasAdmin.js";
import { useColecoesParaSelecao } from "@/hooks/admin/useColecoesAdmin.js";
import { useCoresAtivas } from "@/hooks/admin/useCoresAdmin.js";
import { useTabelasParaSelecao } from "@/hooks/admin/useTabelasMedidasAdmin.js";
import { useTamanhosAtivos } from "@/hooks/admin/useTamanhosAdmin.js";
import { useMutacoesMidia } from "@/hooks/admin/useMidiasAdmin.js";
import {
    useGaleriaProduto,
    useMutacoesGaleria,
    useMutacoesProduto,
    useMutacoesVariacao,
    useProdutoAdmin,
    useVariacoesProduto,
} from "@/hooks/admin/useProdutosAdmin.js";
import { useToast } from "@/hooks/useToast.js";
import {
    GENEROS,
    GENERO_PRODUTO,
    LIMITES,
    MODELAGENS,
} from "@/lib/dominioCatalogo.js";
import { formatarCentavosParaBRL } from "@/utils/financeiro.js";

/**
 * Formulário da peça — o coração do painel.
 *
 * Ele é dividido em três abas porque a API é dividida assim: variação e mídia
 * pendem de um produto que já tem id (`POST /produtos/{id}/variacoes`,
 * `POST /produtos/{id}/midias`). Numa peça nova só a aba de dados existe; ao
 * salvar, a tela navega para a edição e as outras duas abrem. Fingir um
 * formulário único e guardar tudo em memória para enviar no fim daria uma
 * gravação parcial silenciosa quando qualquer lote falhasse no meio.
 */
const ABAS = {
    DADOS: "dados",
    VARIACOES: "variacoes",
    IMAGENS: "imagens",
};

const FORM_VAZIO = {
    nome: "",
    slug: "",
    skuBase: "",
    descricao: "",
    idCategoria: "",
    genero: GENERO_PRODUTO.FEMININO,
    precoBaseCentavos: null,
    precoComparativoCentavos: null,
    composicaoTecido: "",
    instrucoesLavagem: "",
    modelagem: "",
    idTabelaMedidas: "",
    destaque: false,
    metaTitle: "",
    metaDescription: "",
    idsColecoes: [],
};

function produtoParaForm(produto) {
    return {
        nome: produto.nome ?? "",
        slug: produto.slug ?? "",
        skuBase: produto.skuBase ?? "",
        descricao: produto.descricao ?? "",
        idCategoria: produto.idCategoria ?? "",
        genero: produto.genero ?? GENERO_PRODUTO.FEMININO,
        precoBaseCentavos: produto.precoBaseCentavos ?? null,
        precoComparativoCentavos: produto.precoComparativoCentavos ?? null,
        composicaoTecido: produto.composicaoTecido ?? "",
        instrucoesLavagem: produto.instrucoesLavagem ?? "",
        modelagem: produto.modelagem ?? "",
        idTabelaMedidas: produto.idTabelaMedidas ?? "",
        destaque: !!produto.destaque,
        metaTitle: produto.metaTitle ?? "",
        metaDescription: produto.metaDescription ?? "",
        idsColecoes: (produto.colecoes ?? []).map((c) => c.id),
    };
}

/**
 * Validação de tela. Espelha os `[Required]`/`[Range]`/`[StringLength]` do
 * `ProdutoCreateDto` e acrescenta duas regras que o DTO não tem, mas que a loja
 * precisa: preço maior que zero e preço comparativo acima do preço de venda —
 * um "de R$ 80 por R$ 90" riscado é propaganda enganosa, não merchandising.
 */
function validarForm(form) {
    const erros = {};
    const nome = form.nome.trim();
    const sku = form.skuBase.trim();

    if (nome.length < 2) erros.nome = "O nome deve ter entre 2 e 180 caracteres.";
    if (sku.length < 2) erros.skuBase = "O SKU base deve ter entre 2 e 60 caracteres.";
    if (!form.idCategoria) erros.idCategoria = "Escolha a categoria da peça.";

    if (form.precoBaseCentavos === null)
        erros.precoBaseCentavos = "Informe o preço de venda da peça.";
    else if (form.precoBaseCentavos <= 0)
        erros.precoBaseCentavos = "O preço de venda precisa ser maior que zero.";

    if (
        form.precoComparativoCentavos !== null &&
        form.precoBaseCentavos !== null &&
        form.precoComparativoCentavos <= form.precoBaseCentavos
    ) {
        erros.precoComparativoCentavos =
            "O preço riscado precisa ser maior que o preço de venda.";
    }

    return erros;
}

function formParaPayload(form) {
    return {
        nome: form.nome.trim(),
        slug: form.slug.trim() || null,
        skuBase: form.skuBase.trim(),
        descricao: form.descricao.trim() || null,
        idCategoria: Number(form.idCategoria),
        genero: Number(form.genero),
        precoBaseCentavos: form.precoBaseCentavos ?? 0,
        precoComparativoCentavos: form.precoComparativoCentavos,
        composicaoTecido: form.composicaoTecido.trim() || null,
        instrucoesLavagem: form.instrucoesLavagem.trim() || null,
        modelagem: form.modelagem === "" ? null : Number(form.modelagem),
        idTabelaMedidas: form.idTabelaMedidas === "" ? null : Number(form.idTabelaMedidas),
        destaque: form.destaque,
        metaTitle: form.metaTitle.trim() || null,
        metaDescription: form.metaDescription.trim() || null,
        idsColecoes: form.idsColecoes,
    };
}

// ---------------------------------------------------------------------------

export default function FormProduto() {
    const { id } = useParams();
    const navegar = useNavigate();
    const toast = useToast();

    const ehNova = !id;
    const idProduto = ehNova ? null : Number(id);

    const [aba, setAba] = useState(ABAS.DADOS);
    const [form, setForm] = useState(FORM_VAZIO);
    const [erros, setErros] = useState({});
    const [incluirInativas, setIncluirInativas] = useState(false);
    const [confirmar, setConfirmar] = useState(null);
    const [salvandoVariacao, setSalvandoVariacao] = useState(null);

    const { produto, isLoading, isError } = useProdutoAdmin(idProduto);
    const { opcoes: categorias } = useArvoreCategorias(false);
    const { colecoes } = useColecoesParaSelecao();
    const { tabelas } = useTabelasParaSelecao();
    const { tamanhos } = useTamanhosAtivos();
    const { cores } = useCoresAtivas();

    const { criar, atualizar } = useMutacoesProduto();
    const variacoesMut = useMutacoesVariacao(idProduto);
    const galeriaMut = useMutacoesGaleria(idProduto);
    const { enviar: enviarMidia } = useMutacoesMidia();

    const { variacoes, isLoading: carregandoVariacoes } = useVariacoesProduto(
        idProduto,
        incluirInativas,
    );
    const { galeria, isLoading: carregandoGaleria } = useGaleriaProduto(idProduto);

    useEffect(() => {
        if (produto) setForm(produtoParaForm(produto));
    }, [produto]);

    const setCampo = (campo, valor) => setForm((atual) => ({ ...atual, [campo]: valor }));

    const alternarColecao = (idColecao) =>
        setForm((atual) => ({
            ...atual,
            idsColecoes: atual.idsColecoes.includes(idColecao)
                ? atual.idsColecoes.filter((x) => x !== idColecao)
                : [...atual.idsColecoes, idColecao],
        }));

    const salvando = criar.isPending || atualizar.isPending;

    const submeter = async (evento) => {
        evento.preventDefault();

        const encontrados = validarForm(form);
        setErros(encontrados);

        if (Object.keys(encontrados).length > 0) {
            setAba(ABAS.DADOS);
            return;
        }

        const payload = formParaPayload(form);

        try {
            if (ehNova) {
                const criada = await criar.mutateAsync(payload);
                toast.success("Peça criada. Agora monte a grade de tamanhos e cores.");
                navegar(`/admin/produtos/${criada.id}`, { replace: true });
                setAba(ABAS.VARIACOES);
            } else {
                await atualizar.mutateAsync({ id: idProduto, payload });
                toast.success("Dados da peça salvos.");
            }
        } catch {
            // Erro de API já virou toast no interceptor. O formulário permanece
            // preenchido para o operador corrigir e reenviar.
        }
    };

    // --------------------------------------------------------- Variações

    const gerarGrade = async (payload) => {
        try {
            const resultado = await variacoesMut.gerarGrade.mutateAsync(payload);
            const criadas = resultado?.criadas ?? 0;
            const existentes = resultado?.jaExistiam ?? 0;

            if (criadas === 0) {
                toast.info("Todas as combinações escolhidas já existiam na grade.");
            } else {
                toast.success(
                    `${criadas} ${criadas === 1 ? "variação criada" : "variações criadas"}` +
                        (existentes > 0 ? ` · ${existentes} já existiam e foram mantidas.` : "."),
                );
            }
        } catch {
            /* toast de erro já emitido pelo interceptor */
        }
    };

    const salvarVariacao = async (idVariacao, payload) => {
        setSalvandoVariacao(idVariacao);
        try {
            await variacoesMut.atualizar.mutateAsync({ id: idVariacao, payload });
            toast.success("Variação salva.");
        } catch {
            /* toast de erro já emitido pelo interceptor */
        } finally {
            setSalvandoVariacao(null);
        }
    };

    // ----------------------------------------------------------- Galeria

    const enviarFoto = async ({ arquivo, altText, idCor }) => {
        try {
            const midia = await enviarMidia.mutateAsync({ arquivo, altText });
            await galeriaMut.vincular.mutateAsync({
                idMidia: midia.id,
                idCor,
                ordem: galeria.length,
                ehCapa: galeria.length === 0, // a primeira foto da peça já nasce capa
            });
            toast.success("Imagem enviada para a galeria.");
        } catch {
            /* toast de erro já emitido pelo interceptor */
        }
    };

    const trocarCorDaFoto = async ({ item, idCor }) => {
        if ((item.idCor ?? null) === idCor) return;
        try {
            await galeriaMut.alterarCor.mutateAsync({ item, idCor });
            toast.success("Cor da foto atualizada.");
        } catch {
            /* toast de erro já emitido pelo interceptor */
        }
    };

    // -------------------------------------------------------- Confirmações

    const confirmarAcao = async () => {
        if (!confirmar) return;

        try {
            if (confirmar.tipo === "removerFoto") {
                await galeriaMut.desvincular.mutateAsync(confirmar.foto.idMidia);
                toast.success("Imagem removida da galeria.");
            } else if (confirmar.tipo === "desativarVariacao") {
                await variacoesMut.desativar.mutateAsync(confirmar.variacao.id);
                toast.success("SKU desativado.");
            } else if (confirmar.tipo === "ativarVariacao") {
                await variacoesMut.ativar.mutateAsync(confirmar.variacao.id);
                toast.success("SKU reativado.");
            }
            setConfirmar(null);
        } catch {
            /* toast de erro já emitido pelo interceptor */
        }
    };

    const textoConfirmacao = useMemo(() => {
        if (!confirmar) return {};
        if (confirmar.tipo === "removerFoto") {
            return {
                titulo: "Remover a imagem da galeria",
                mensagem:
                    "A foto sai desta peça. O arquivo continua no acervo de mídias e pode ser usado em outra peça. Se ela for a capa, a seguinte assume o lugar.",
                textoConfirmar: "Remover",
                variante: "perigo",
            };
        }
        if (confirmar.tipo === "desativarVariacao") {
            return {
                titulo: "Desativar o SKU",
                mensagem: `O SKU ${confirmar.variacao.sku} deixa de ser vendido. Ele continua existindo, porque aparece em pedidos e etiquetas já emitidos, e pode ser reativado depois.`,
                textoConfirmar: "Desativar",
                variante: "perigo",
            };
        }
        return {
            titulo: "Reativar o SKU",
            mensagem: `O SKU ${confirmar.variacao?.sku} volta a ser vendido, respeitando o saldo em estoque.`,
            textoConfirmar: "Reativar",
            variante: "primario",
        };
    }, [confirmar]);

    // -------------------------------------------------------------- Guardas

    if (!ehNova && isLoading) {
        return (
            <div className="animate-fade-up">
                <Skeleton className="h-4 w-24" />
                <Skeleton className="mt-4 h-8 w-72" />
                <div className="mt-10 flex flex-col gap-4">
                    {Array.from({ length: 6 }).map((_, i) => (
                        <Skeleton key={`sk-${i}`} className="h-12 w-full" />
                    ))}
                </div>
            </div>
        );
    }

    if (!ehNova && (isError || !produto)) {
        return (
            <div className="animate-fade-up">
                <CabecalhoPagina sobrancelha="Catálogo" titulo="Peça não encontrada" />
                <Aviso
                    variante="erro"
                    titulo="Esta peça não existe ou não está mais acessível"
                    acoes={
                        <Botao variante="contorno" tamanho="sm" to="/admin/produtos">
                            Voltar para a lista
                        </Botao>
                    }
                >
                    <p>
                        Verifique o endereço. Se a peça foi removida do catálogo, ela não aparece
                        nem entre as que estão fora do ar.
                    </p>
                </Aviso>
            </div>
        );
    }

    const abas = [
        { id: ABAS.DADOS, rotulo: "Dados da peça", Icone: FiSliders },
        {
            id: ABAS.VARIACOES,
            rotulo: "Variações",
            Icone: FiGrid,
            contador: produto?.totalVariacoes,
        },
        { id: ABAS.IMAGENS, rotulo: "Imagens", Icone: FiImage, contador: galeria.length },
    ];

    return (
        <div className="animate-fade-up">
            <Link
                to="/admin/produtos"
                className="mb-6 inline-flex items-center gap-2 font-sans text-xs uppercase tracking-widest text-ink-soft transition-colors hover:text-ink"
            >
                <FiArrowLeft size={14} aria-hidden="true" />
                Todas as peças
            </Link>

            <CabecalhoPagina
                sobrancelha="Catálogo"
                titulo={ehNova ? "Nova peça" : produto.nome}
                descricao={
                    ehNova
                        ? "Comece pelos dados de vitrine. Assim que a peça for salva, a grade de tamanhos e cores e a galeria de fotos abrem nas abas ao lado."
                        : `SKU base ${produto.skuBase} · ${produto.totalVariacoes} ${produto.totalVariacoes === 1 ? "SKU" : "SKUs"} · ${produto.estoqueTotalDisponivel} em estoque disponível`
                }
                acoes={
                    !ehNova && (
                        <Badge variante={produto.ativo ? "neutro" : "esgotado"}>
                            {produto.ativo ? "Publicada" : "Fora do ar"}
                        </Badge>
                    )
                }
            />

            {/* ----------------------------------------------------- Abas */}
            <div role="tablist" aria-label="Seções da peça" className="mb-8 flex flex-wrap gap-1 border-b border-sand">
                {abas.map(({ id: idAba, rotulo, Icone, contador }) => {
                    const bloqueada = ehNova && idAba !== ABAS.DADOS;
                    const ativa = aba === idAba;

                    return (
                        <button
                            key={idAba}
                            type="button"
                            role="tab"
                            aria-selected={ativa}
                            disabled={bloqueada}
                            title={
                                bloqueada
                                    ? "Salve os dados da peça para liberar esta seção."
                                    : undefined
                            }
                            onClick={() => setAba(idAba)}
                            className={`-mb-px flex items-center gap-2 border-b-2 px-4 py-3 font-sans text-xs uppercase tracking-widest transition-colors disabled:cursor-not-allowed disabled:opacity-40 ${
                                ativa
                                    ? "border-olive text-ink"
                                    : "border-transparent text-ink-soft hover:text-ink"
                            }`}
                        >
                            <Icone size={14} aria-hidden="true" />
                            {rotulo}
                            {contador != null && contador > 0 && (
                                <span className="preco text-taupe">{contador}</span>
                            )}
                        </button>
                    );
                })}
            </div>

            {ehNova && (
                <Aviso variante="info" className="mb-8">
                    <p>
                        A grade de variações e as fotos pertencem a uma peça que já existe no
                        banco. Salve os dados abaixo e as duas seções abrem em seguida.
                    </p>
                </Aviso>
            )}

            {/* ---------------------------------------------------- Dados */}
            {aba === ABAS.DADOS && (
                <form onSubmit={submeter} noValidate className="flex flex-col gap-10">
                    <fieldset className="flex flex-col gap-6">
                        <legend className="mb-2 font-display text-xl tracking-tight text-ink">
                            Identificação
                        </legend>

                        <div className="grid grid-cols-1 gap-6 lg:grid-cols-2">
                            <Campo
                                label="Nome da peça"
                                obrigatorio
                                value={form.nome}
                                erro={erros.nome}
                                maxLength={LIMITES.produtoNome}
                                placeholder="Vestido Midi Linho"
                                onChange={(e) => setCampo("nome", e.target.value)}
                            />
                            <Campo
                                label="SKU base"
                                obrigatorio
                                value={form.skuBase}
                                erro={erros.skuBase}
                                maxLength={LIMITES.produtoSkuBase}
                                placeholder="VST-MIDI-LIN"
                                ajuda="É o SKU do modelo. O SKU vendável nasce na variação, a partir deste prefixo."
                                onChange={(e) => setCampo("skuBase", e.target.value)}
                            />
                        </div>

                        <div className="grid grid-cols-1 gap-6 lg:grid-cols-2">
                            <Campo
                                label="Endereço na loja"
                                value={form.slug}
                                maxLength={LIMITES.produtoSlug}
                                placeholder="vestido-midi-linho"
                                ajuda={
                                    ehNova
                                        ? "Em branco, o endereço é gerado a partir do nome."
                                        : "Em branco, mantém o endereço atual. Trocar quebra o link que já está indexado."
                                }
                                onChange={(e) => setCampo("slug", e.target.value)}
                            />
                            <Campo
                                label="Categoria"
                                como="select"
                                obrigatorio
                                value={form.idCategoria}
                                erro={erros.idCategoria}
                                onChange={(e) => setCampo("idCategoria", e.target.value)}
                            >
                                <option value="">Escolha a categoria</option>
                                {categorias.map((categoria) => (
                                    <option key={categoria.id} value={categoria.id}>
                                        {"— ".repeat(categoria.profundidade)}
                                        {categoria.nome}
                                    </option>
                                ))}
                            </Campo>
                        </div>

                        <Campo
                            label="Descrição"
                            como="textarea"
                            rows={5}
                            value={form.descricao}
                            placeholder="O caimento, o tecido, a ocasião. Escreva como quem apresenta a peça na loja."
                            onChange={(e) => setCampo(CAMPO.descricao, e.target.value)}
                        />
                    </fieldset>

                    <fieldset className="flex flex-col gap-6 border-t border-sand pt-8">
                        <legend className="mb-2 font-display text-xl tracking-tight text-ink">
                            Preço
                        </legend>

                        <div className="grid grid-cols-1 gap-6 lg:grid-cols-2">
                            <CampoDinheiro
                                label="Preço de venda"
                                obrigatorio
                                valorCentavos={form.precoBaseCentavos}
                                erro={erros.precoBaseCentavos}
                                ajuda="Vale para toda a grade, a menos que a variação tenha preço próprio."
                                onChange={(v) => setCampo("precoBaseCentavos", v)}
                            />
                            <CampoDinheiro
                                label="Preço riscado"
                                valorCentavos={form.precoComparativoCentavos}
                                erro={erros.precoComparativoCentavos}
                                ajuda={`O "de" que aparece cortado na vitrine. ${
                                    form.precoBaseCentavos
                                        ? `Precisa ser maior que ${formatarCentavosParaBRL(form.precoBaseCentavos)}.`
                                        : "Deixe em branco quando não houver."
                                }`}
                                onChange={(v) => setCampo("precoComparativoCentavos", v)}
                            />
                        </div>
                    </fieldset>

                    <fieldset className="flex flex-col gap-6 border-t border-sand pt-8">
                        <legend className="mb-2 font-display text-xl tracking-tight text-ink">
                            Ficha da peça
                        </legend>

                        <div className="grid grid-cols-1 gap-6 lg:grid-cols-3">
                            <Campo
                                label="Gênero"
                                como="select"
                                value={form.genero}
                                onChange={(e) => setCampo("genero", e.target.value)}
                            >
                                {GENEROS.map(({ valor, rotulo }) => (
                                    <option key={valor} value={valor}>
                                        {rotulo}
                                    </option>
                                ))}
                            </Campo>

                            <Campo
                                label="Modelagem"
                                como="select"
                                value={form.modelagem}
                                onChange={(e) => setCampo("modelagem", e.target.value)}
                            >
                                <option value="">Não informar</option>
                                {MODELAGENS.map(({ valor, rotulo }) => (
                                    <option key={valor} value={valor}>
                                        {rotulo}
                                    </option>
                                ))}
                            </Campo>

                            <Campo
                                label="Guia de medidas"
                                como="select"
                                value={form.idTabelaMedidas}
                                ajuda="Reduz devolução por tamanho errado."
                                onChange={(e) => setCampo("idTabelaMedidas", e.target.value)}
                            >
                                <option value="">Sem guia de medidas</option>
                                {tabelas.map((tabela) => (
                                    <option key={tabela.id} value={tabela.id}>
                                        {tabela.nome}
                                    </option>
                                ))}
                            </Campo>
                        </div>

                        <Campo
                            label="Composição do tecido"
                            value={form.composicaoTecido}
                            maxLength={LIMITES.composicaoTecido}
                            placeholder="100% linho"
                            onChange={(e) => setCampo("composicaoTecido", e.target.value)}
                        />

                        <Campo
                            label="Instruções de lavagem"
                            como="textarea"
                            rows={3}
                            value={form.instrucoesLavagem}
                            placeholder="Lavar à mão em água fria. Secar à sombra."
                            onChange={(e) => setCampo("instrucoesLavagem", e.target.value)}
                        />

                        <label className="flex items-center gap-2 font-sans text-sm text-ink">
                            <input
                                type="checkbox"
                                checked={form.destaque}
                                onChange={(e) => setCampo("destaque", e.target.checked)}
                                className="h-4 w-4 accent-olive"
                            />
                            Mostrar entre os destaques da home
                        </label>
                    </fieldset>

                    <fieldset className="flex flex-col gap-4 border-t border-sand pt-8">
                        <legend className="mb-2 font-display text-xl tracking-tight text-ink">
                            Coleções
                        </legend>
                        <p className="text-sm text-ink-soft">
                            Coleção é curadoria, não taxonomia: a mesma peça pode abrir um drop e
                            continuar na categoria de sempre.
                        </p>

                        {colecoes.length === 0 ? (
                            <p className="text-sm text-taupe">
                                Nenhuma coleção cadastrada ainda.{" "}
                                <Link
                                    to="/admin/colecoes"
                                    className="underline underline-offset-4 hover:text-ink"
                                >
                                    Criar a primeira
                                </Link>
                            </p>
                        ) : (
                            <div className="flex flex-wrap gap-2">
                                {colecoes.map((colecao) => {
                                    const marcada = form.idsColecoes.includes(colecao.id);
                                    return (
                                        <button
                                            key={colecao.id}
                                            type="button"
                                            aria-pressed={marcada}
                                            onClick={() => alternarColecao(colecao.id)}
                                            className={`h-11 border px-4 font-sans text-xs uppercase tracking-widest transition-colors ${
                                                marcada
                                                    ? "border-olive bg-olive text-bone"
                                                    : "border-sand bg-base-100 text-ink-soft hover:border-ink"
                                            }`}
                                        >
                                            {colecao.nome}
                                        </button>
                                    );
                                })}
                            </div>
                        )}
                    </fieldset>

                    <fieldset className="flex flex-col gap-6 border-t border-sand pt-8">
                        <legend className="mb-2 font-display text-xl tracking-tight text-ink">
                            Busca e compartilhamento
                        </legend>

                        <Campo
                            label="Título para o buscador"
                            value={form.metaTitle}
                            maxLength={LIMITES.metaTitle}
                            ajuda="Em branco, o buscador usa o nome da peça."
                            onChange={(e) => setCampo("metaTitle", e.target.value)}
                        />
                        <Campo
                            label="Resumo para o buscador"
                            como="textarea"
                            rows={3}
                            value={form.metaDescription}
                            maxLength={LIMITES.metaDescription}
                            onChange={(e) => setCampo("metaDescription", e.target.value)}
                        />
                    </fieldset>

                    <div className="sticky bottom-0 flex flex-wrap items-center justify-end gap-3 border-t border-sand bg-base-100 py-4">
                        <Botao variante="texto" to="/admin/produtos">
                            Cancelar
                        </Botao>
                        <Botao type="submit" carregando={salvando} disabled={salvando}>
                            <FiSave size={14} aria-hidden="true" />
                            {ehNova ? "Salvar e continuar" : "Salvar alterações"}
                        </Botao>
                    </div>
                </form>
            )}

            {/* ------------------------------------------------- Variações */}
            {aba === ABAS.VARIACOES && !ehNova && (
                <MatrizVariacoes
                    variacoes={variacoes}
                    tamanhos={tamanhos}
                    cores={cores}
                    precoBaseCentavos={form.precoBaseCentavos ?? 0}
                    skuBase={form.skuBase}
                    carregando={carregandoVariacoes}
                    incluirInativas={incluirInativas}
                    onIncluirInativas={setIncluirInativas}
                    onGerarGrade={gerarGrade}
                    gerando={variacoesMut.gerarGrade.isPending}
                    onSalvarLinha={salvarVariacao}
                    salvandoId={salvandoVariacao}
                    onDesativar={(variacao) =>
                        setConfirmar({ tipo: "desativarVariacao", variacao })
                    }
                    onAtivar={(variacao) => setConfirmar({ tipo: "ativarVariacao", variacao })}
                />
            )}

            {/* --------------------------------------------------- Imagens */}
            {aba === ABAS.IMAGENS && !ehNova && (
                <UploadImagens
                    galeria={galeria}
                    cores={cores}
                    carregando={carregandoGaleria}
                    enviando={enviarMidia.isPending || galeriaMut.vincular.isPending}
                    onEnviar={enviarFoto}
                    onRemover={(foto) => setConfirmar({ tipo: "removerFoto", foto })}
                    onReordenar={(ids) => galeriaMut.reordenar.mutate(ids)}
                    onTrocarCor={trocarCorDaFoto}
                />
            )}

            <ConfirmModal
                isOpen={!!confirmar}
                titulo={textoConfirmacao.titulo}
                mensagem={textoConfirmacao.mensagem}
                textoConfirmar={textoConfirmacao.textoConfirmar}
                variante={textoConfirmacao.variante}
                carregando={
                    galeriaMut.desvincular.isPending ||
                    variacoesMut.desativar.isPending ||
                    variacoesMut.ativar.isPending
                }
                onConfirm={confirmarAcao}
                onCancel={() => setConfirmar(null)}
            />
        </div>
    );
}
