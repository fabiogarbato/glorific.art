import { Link } from "react-router-dom";
import { FiAlertTriangle, FiMinus, FiPlus, FiTrash2 } from "react-icons/fi";

import { formatarCentavosParaBRL } from "@/utils/financeiro.js";

/** Teto por linha espelhando `CarrinhoItemCreateDto` (Range 1..20) no backend. */
const QUANTIDADE_MAXIMA = 20;

/**
 * Uma linha do carrinho.
 *
 * O item vem de `CarrinhoItemResponseDto`. Repare no que a API NAO manda hoje:
 * URL de imagem. Enquanto ela nao existir no contrato, a miniatura e a amostra
 * da cor (`corHexRgb`) — inventar um caminho de foto aqui daria imagem quebrada
 * em toda a tela.
 *
 * Preco: `precoUnitarioAtualCentavos` e o que SERA cobrado;
 * `precoUnitarioSnapshotCentavos` e o que valia quando a peça entrou. Quando os
 * dois divergem o carrinho avisa em vez de corrigir calado — cobrar surpresa e
 * pior, e apagar a linha sem explicação e pior ainda.
 */
export default function ItemCarrinho({ item, onAlterarQuantidade, onRemover, salvando = false }) {
    const {
        nomeProduto,
        slugProduto,
        sku,
        tamanho,
        cor,
        corHexRgb,
        quantidade,
        precoUnitarioAtualCentavos,
        precoUnitarioSnapshotCentavos,
        precoAlterado,
        totalLinhaCentavos,
        disponivelEmEstoque,
        indisponivel,
        quantidadeAcimaDoDisponivel,
        imagemUrl,
    } = item;

    const encareceu = precoUnitarioAtualCentavos > precoUnitarioSnapshotCentavos;
    const tetoDaLinha = Math.min(QUANTIDADE_MAXIMA, Math.max(disponivelEmEstoque || 0, 1));

    return (
        <li className="flex gap-4 border-b border-sand py-6 sm:gap-6">
            {/* ------------------------------------------------------- miniatura */}
            <div className="w-24 shrink-0 sm:w-28">
                {imagemUrl ? (
                    <img
                        src={imagemUrl}
                        alt={nomeProduto}
                        loading="lazy"
                        className={`aspect-product w-full object-cover ${indisponivel ? "opacity-40" : ""}`}
                    />
                ) : (
                    <div
                        aria-hidden="true"
                        className={`aspect-product w-full border border-sand ${indisponivel ? "opacity-40" : ""}`}
                        style={{ backgroundColor: corHexRgb || "var(--linen)" }}
                    />
                )}
            </div>

            {/* ---------------------------------------------------------- corpo */}
            <div className="flex min-w-0 flex-1 flex-col gap-3">
                <div className="flex items-start justify-between gap-4">
                    <div className="min-w-0">
                        <h3 className="font-display text-lg leading-snug tracking-tight text-ink">
                            {slugProduto ? (
                                <Link to={`/produto/${slugProduto}`} className="hover:text-olive">
                                    {nomeProduto}
                                </Link>
                            ) : (
                                nomeProduto
                            )}
                        </h3>

                        <p className="mt-1 text-sm text-ink-soft">
                            {[tamanho && `Tamanho ${tamanho}`, cor].filter(Boolean).join(" · ") ||
                                sku}
                        </p>
                    </div>

                    <button
                        type="button"
                        onClick={() => onRemover(item)}
                        disabled={salvando}
                        aria-label={`Remover ${nomeProduto} do carrinho`}
                        className="-mt-1 flex h-11 w-11 shrink-0 items-center justify-center text-taupe transition-colors hover:text-danger disabled:opacity-40"
                    >
                        <FiTrash2 size={17} />
                    </button>
                </div>

                {/* ------------------------------------------------------ avisos */}
                {indisponivel && (
                    <p
                        role="status"
                        className="flex items-start gap-2 border-l-2 border-danger bg-linen px-3 py-2 text-sm text-danger"
                    >
                        <FiAlertTriangle size={15} className="mt-0.5 shrink-0" aria-hidden="true" />
                        Esta peça ficou indisponível. Remova para concluir o pedido.
                    </p>
                )}

                {!indisponivel && quantidadeAcimaDoDisponivel && (
                    <p
                        role="status"
                        className="flex items-start gap-2 border-l-2 border-warning bg-linen px-3 py-2 text-sm text-ink-soft"
                    >
                        <FiAlertTriangle size={15} className="mt-0.5 shrink-0" aria-hidden="true" />
                        Restam {disponivelEmEstoque} em estoque. Ajuste a quantidade.
                    </p>
                )}

                {precoAlterado && (
                    <p
                        role="status"
                        className="border-l-2 border-brass bg-linen px-3 py-2 text-sm text-ink-soft"
                    >
                        O preço desta peça {encareceu ? "subiu" : "baixou"} desde que você a
                        colocou no carrinho: era{" "}
                        <span className="preco line-through">
                            {formatarCentavosParaBRL(precoUnitarioSnapshotCentavos)}
                        </span>{" "}
                        e agora é{" "}
                        <span className="preco text-ink">
                            {formatarCentavosParaBRL(precoUnitarioAtualCentavos)}
                        </span>
                        . Vale o valor atual.
                    </p>
                )}

                {/* --------------------------------------- quantidade e subtotal */}
                <div className="mt-auto flex flex-wrap items-center justify-between gap-4">
                    <div className="flex items-center border border-sand">
                        <button
                            type="button"
                            onClick={() => onAlterarQuantidade(item, quantidade - 1)}
                            disabled={salvando || quantidade <= 1}
                            aria-label={`Diminuir quantidade de ${nomeProduto}`}
                            className="flex h-10 w-10 items-center justify-center text-ink transition-colors hover:bg-linen disabled:opacity-30"
                        >
                            <FiMinus size={14} />
                        </button>

                        <span
                            className="preco w-10 text-center text-sm text-ink"
                            aria-live="polite"
                            aria-label={`Quantidade: ${quantidade}`}
                        >
                            {quantidade}
                        </span>

                        <button
                            type="button"
                            onClick={() => onAlterarQuantidade(item, quantidade + 1)}
                            disabled={salvando || indisponivel || quantidade >= tetoDaLinha}
                            aria-label={`Aumentar quantidade de ${nomeProduto}`}
                            className="flex h-10 w-10 items-center justify-center text-ink transition-colors hover:bg-linen disabled:opacity-30"
                        >
                            <FiPlus size={14} />
                        </button>
                    </div>

                    <div className="text-right">
                        <p className="preco text-base text-ink">
                            {formatarCentavosParaBRL(totalLinhaCentavos)}
                        </p>
                        {quantidade > 1 && (
                            <p className="preco mt-0.5 text-xs text-ink-soft">
                                {quantidade} × {formatarCentavosParaBRL(precoUnitarioAtualCentavos)}
                            </p>
                        )}
                    </div>
                </div>
            </div>
        </li>
    );
}
