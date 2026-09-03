import { useEffect, useState } from "react";
import Botao from "@/components/ui/Botao.jsx";
import Skeleton from "@/components/ui/Skeleton.jsx";
import {
    alternarNaLista,
    contarFiltrosAtivos,
    GENEROS,
} from "@/lib/vitrine.js";
import {
    formatarCentavosParaBRL,
    mascaraPrecoCentavos,
    parseBRLParaCentavos,
} from "@/utils/financeiro.js";

/**
 * Painel de refino da vitrine.
 *
 * As contagens vem das FACETAS do backend, no mesmo recorte da listagem: sem
 * elas o cliente marca "GG" e recebe zero resultado, que e a pior tela possivel.
 *
 * Um valor ja marcado que sumiu das facetas continua na lista com contagem zero
 * — se ele desaparecesse, nao haveria como desmarcar o filtro que esvaziou a
 * propria vitrine.
 */

function Secao({ titulo, children }) {
    return (
        <fieldset className="border-t border-sand pt-5">
            <legend className="eyebrow px-0 pb-3">{titulo}</legend>
            {children}
        </fieldset>
    );
}

function LinhaEscolha({ tipo, nome, marcado, aoMudar, children, contagem }) {
    return (
        <label className="flex cursor-pointer items-center gap-3 py-1.5 text-sm text-ink-soft transition-colors hover:text-ink">
            <input
                type={tipo}
                name={nome}
                checked={marcado}
                onChange={aoMudar}
                className="h-4 w-4 shrink-0 accent-[#6B7256]"
            />
            <span className="flex-1">{children}</span>
            {contagem != null && (
                <span className="text-xs tabular-nums text-taupe">{contagem}</span>
            )}
        </label>
    );
}

/** Garante que o valor marcado apareca mesmo quando a faceta parou de conta-lo. */
function comSelecionados(itens = [], selecionados = []) {
    const conhecidos = new Set(itens.map((i) => i.valor));
    const orfaos = selecionados
        .filter((v) => !conhecidos.has(v))
        .map((v) => ({ id: `sel-${v}`, valor: v, rotulo: v, total: 0 }));
    return [...itens, ...orfaos];
}

export default function FiltrosCatalogo({
    facetas,
    filtros,
    onAlterar,
    onLimpar,
    carregando = false,
    esconderCategoria = false,
    esconderColecao = false,
}) {
    const [precoMin, setPrecoMin] = useState("");
    const [precoMax, setPrecoMax] = useState("");

    // A URL manda: colar um link com faixa de preco precisa preencher os campos.
    useEffect(() => {
        setPrecoMin(filtros.precoMin ? mascaraPrecoCentavos(String(filtros.precoMin)) : "");
        setPrecoMax(filtros.precoMax ? mascaraPrecoCentavos(String(filtros.precoMax)) : "");
    }, [filtros.precoMin, filtros.precoMax]);

    if (carregando && !facetas) {
        return (
            <div className="flex flex-col gap-6">
                {[0, 1, 2].map((i) => (
                    <div key={i} className="flex flex-col gap-2">
                        <Skeleton className="h-3 w-24" />
                        <Skeleton className="h-4 w-full" />
                        <Skeleton className="h-4 w-5/6" />
                        <Skeleton className="h-4 w-2/3" />
                    </div>
                ))}
            </div>
        );
    }

    if (!facetas) {
        return (
            <p className="text-sm text-ink-soft">
                Não foi possível carregar os filtros agora.
            </p>
        );
    }

    const ativos = contarFiltrosAtivos(filtros);

    const categorias = facetas.categorias ?? [];
    const colecoes = facetas.colecoes ?? [];
    const tamanhos = comSelecionados(facetas.tamanhos, filtros.tamanhos);
    const cores = comSelecionados(facetas.cores, filtros.cores);

    const aplicarPreco = () => {
        const min = parseBRLParaCentavos(precoMin) || null;
        const max = parseBRLParaCentavos(precoMax) || null;
        // Invertido pelo cliente: troca em vez de devolver zero resultado.
        const ordenado = min && max && min > max ? [max, min] : [min, max];
        onAlterar({ precoMin: ordenado[0], precoMax: ordenado[1] });
    };

    return (
        <div className="flex flex-col gap-5">
            <div className="flex items-baseline justify-between gap-3">
                <p className="eyebrow">Refinar</p>
                {ativos > 0 && (
                    <button
                        type="button"
                        onClick={onLimpar}
                        className="text-xs uppercase tracking-widest text-ink-soft underline underline-offset-4 transition-colors hover:text-ink"
                    >
                        Limpar ({ativos})
                    </button>
                )}
            </div>

            {!esconderCategoria && categorias.length > 0 && (
                <Secao titulo="Categoria">
                    <LinhaEscolha
                        tipo="radio"
                        nome="filtro-categoria"
                        marcado={!filtros.categoria}
                        aoMudar={() => onAlterar({ categoria: "" })}
                    >
                        Todas
                    </LinhaEscolha>
                    {categorias.map((item) => (
                        <LinhaEscolha
                            key={item.valor}
                            tipo="radio"
                            nome="filtro-categoria"
                            marcado={filtros.categoria === item.valor}
                            aoMudar={() => onAlterar({ categoria: item.valor })}
                            contagem={item.total}
                        >
                            {item.rotulo}
                        </LinhaEscolha>
                    ))}
                </Secao>
            )}

            {!esconderColecao && colecoes.length > 0 && (
                <Secao titulo="Coleção">
                    <LinhaEscolha
                        tipo="radio"
                        nome="filtro-linha"
                        marcado={!filtros.colecao}
                        aoMudar={() => onAlterar({ colecao: "" })}
                    >
                        Todas
                    </LinhaEscolha>
                    {colecoes.map((item) => (
                        <LinhaEscolha
                            key={item.valor}
                            tipo="radio"
                            nome="filtro-linha"
                            marcado={filtros.colecao === item.valor}
                            aoMudar={() => onAlterar({ colecao: item.valor })}
                            contagem={item.total}
                        >
                            {item.rotulo}
                        </LinhaEscolha>
                    ))}
                </Secao>
            )}

            <Secao titulo="Gênero">
                <LinhaEscolha
                    tipo="radio"
                    nome="filtro-genero"
                    marcado={!filtros.genero}
                    aoMudar={() => onAlterar({ genero: "" })}
                >
                    Todos
                </LinhaEscolha>
                {GENEROS.map((g) => (
                    <LinhaEscolha
                        key={g.valor}
                        tipo="radio"
                        nome="filtro-genero"
                        marcado={filtros.genero === g.valor}
                        aoMudar={() => onAlterar({ genero: g.valor })}
                    >
                        {g.rotulo}
                    </LinhaEscolha>
                ))}
            </Secao>

            {tamanhos.length > 0 && (
                <Secao titulo="Tamanho">
                    <div className="flex flex-wrap gap-2">
                        {tamanhos.map((item) => {
                            const marcado = filtros.tamanhos.includes(item.valor);
                            const vazio = item.total === 0;
                            return (
                                <label
                                    key={item.valor}
                                    title={
                                        vazio
                                            ? "Nenhuma peça com os filtros atuais"
                                            : `${item.total} peças`
                                    }
                                    className={[
                                        "inline-flex h-10 min-w-[2.75rem] cursor-pointer items-center justify-center border px-3",
                                        "font-sans text-xs uppercase tracking-widest transition-colors",
                                        marcado
                                            ? "border-ink bg-ink text-bone"
                                            : "border-sand bg-base-100 text-ink hover:border-ink",
                                        vazio && !marcado ? "text-taupe opacity-60" : "",
                                    ].join(" ")}
                                >
                                    <input
                                        type="checkbox"
                                        className="sr-only"
                                        checked={marcado}
                                        onChange={() =>
                                            onAlterar({
                                                tamanhos: alternarNaLista(
                                                    filtros.tamanhos,
                                                    item.valor,
                                                ),
                                            })
                                        }
                                    />
                                    {item.rotulo}
                                </label>
                            );
                        })}
                    </div>
                </Secao>
            )}

            {cores.length > 0 && (
                <Secao titulo="Cor">
                    {cores.map((item) => (
                        <LinhaEscolha
                            key={item.valor}
                            tipo="checkbox"
                            nome="filtro-cor"
                            marcado={filtros.cores.includes(item.valor)}
                            aoMudar={() =>
                                onAlterar({
                                    cores: alternarNaLista(filtros.cores, item.valor),
                                })
                            }
                            contagem={item.total}
                        >
                            <span className="flex items-center gap-2">
                                <span
                                    aria-hidden="true"
                                    className="inline-block h-4 w-4 shrink-0 rounded-full shadow-[inset_0_0_0_1px_rgba(28,26,23,0.12)]"
                                    style={{ backgroundColor: item.hexRgb || "var(--sand)" }}
                                />
                                {item.rotulo}
                            </span>
                        </LinhaEscolha>
                    ))}
                </Secao>
            )}

            <Secao titulo="Faixa de preço">
                <p className="mb-3 text-xs text-ink-soft">
                    A vitrine atual vai de {formatarCentavosParaBRL(facetas.precoMinCentavos)} a{" "}
                    {formatarCentavosParaBRL(facetas.precoMaxCentavos)}.
                </p>

                <div className="flex items-end gap-3">
                    <div className="flex-1">
                        <label
                            htmlFor="filtro-preco-min"
                            className="mb-1 block text-xs text-ink-soft"
                        >
                            De
                        </label>
                        <input
                            id="filtro-preco-min"
                            inputMode="numeric"
                            value={precoMin}
                            onChange={(e) => setPrecoMin(mascaraPrecoCentavos(e.target.value))}
                            placeholder="0,00"
                            className="w-full border border-sand bg-base-100 px-3 py-2 text-sm tabular-nums text-ink placeholder:text-taupe focus:border-olive focus:outline-none"
                        />
                    </div>
                    <div className="flex-1">
                        <label
                            htmlFor="filtro-preco-max"
                            className="mb-1 block text-xs text-ink-soft"
                        >
                            Até
                        </label>
                        <input
                            id="filtro-preco-max"
                            inputMode="numeric"
                            value={precoMax}
                            onChange={(e) => setPrecoMax(mascaraPrecoCentavos(e.target.value))}
                            placeholder="0,00"
                            className="w-full border border-sand bg-base-100 px-3 py-2 text-sm tabular-nums text-ink placeholder:text-taupe focus:border-olive focus:outline-none"
                        />
                    </div>
                </div>

                <Botao
                    variante="contorno"
                    tamanho="sm"
                    className="mt-3"
                    onClick={aplicarPreco}
                >
                    Aplicar preço
                </Botao>
            </Secao>

            <Secao titulo="Disponibilidade">
                <LinhaEscolha
                    tipo="checkbox"
                    nome="filtro-esgotados"
                    marcado={filtros.incluirEsgotados}
                    aoMudar={() => onAlterar({ incluirEsgotados: !filtros.incluirEsgotados })}
                >
                    Mostrar também as peças esgotadas
                </LinhaEscolha>
            </Secao>
        </div>
    );
}
