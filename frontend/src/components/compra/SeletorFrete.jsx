import { FiRefreshCw } from "react-icons/fi";

import Skeleton from "@/components/ui/Skeleton.jsx";
import Botao from "@/components/ui/Botao.jsx";
import { formatarCentavosParaBRL } from "@/utils/financeiro.js";

/**
 * Escolha do serviço de frete.
 *
 * O que o checkout envia de volta é apenas `idServico` — o VALOR é recotado no
 * servidor. Aceitar o preço do frete vindo do navegador foi exatamente o buraco
 * do projeto de referência: trocar um número no devtools comprava frete grátis.
 *
 * `prazoDias` já vem com o manuseio da loja somado; exibir só o prazo da
 * transportadora seria prometer entrega que a expedição não cumpre.
 */
function prazoEmTexto(dias) {
    if (!dias) return "Prazo a confirmar";
    return dias === 1 ? "Chega em 1 dia útil" : `Chega em até ${dias} dias úteis`;
}

export default function SeletorFrete({
    opcoes = [],
    idSelecionado,
    onSelecionar,
    isLoading = false,
    isError = false,
    vazio = false,
    onTentarNovamente,
    titulo = "Entrega",
}) {
    if (isLoading) {
        return (
            <div className="flex flex-col gap-3" aria-busy="true">
                <p className="eyebrow">{titulo}</p>
                <Skeleton className="h-16 w-full" />
                <Skeleton className="h-16 w-full" />
            </div>
        );
    }

    if (isError) {
        return (
            <div className="border border-sand bg-linen px-4 py-5">
                <p className="text-sm text-ink">
                    Não conseguimos calcular o frete agora. A transportadora pode estar fora do ar
                    ou você fez muitas consultas seguidas.
                </p>
                {onTentarNovamente && (
                    <Botao
                        variante="contorno"
                        tamanho="sm"
                        className="mt-4"
                        onClick={onTentarNovamente}
                    >
                        <FiRefreshCw size={14} aria-hidden="true" />
                        Tentar de novo
                    </Botao>
                )}
            </div>
        );
    }

    if (vazio) {
        return (
            <div className="border border-sand bg-linen px-4 py-5">
                <p className="text-sm text-ink-soft">
                    Nenhuma transportadora atende esse CEP com as peças do seu carrinho. Confira o
                    número digitado ou fale com a gente.
                </p>
            </div>
        );
    }

    if (opcoes.length === 0) return null;

    return (
        <fieldset className="flex flex-col gap-2">
            <legend className="eyebrow mb-2">{titulo}</legend>

            {opcoes.map((opcao) => {
                const selecionado = Number(idSelecionado) === Number(opcao.idServico);
                const teveDesconto = opcao.gratis && opcao.valorCotadoCentavos > 0;

                return (
                    <label
                        key={opcao.idServico}
                        className={`flex cursor-pointer items-center gap-3 border px-4 py-3.5 transition-colors ${
                            selecionado
                                ? "border-olive bg-linen"
                                : "border-sand hover:border-taupe"
                        }`}
                    >
                        <input
                            type="radio"
                            name="opcao-frete"
                            value={opcao.idServico}
                            checked={selecionado}
                            onChange={() => onSelecionar(opcao)}
                            className="h-4 w-4 shrink-0 accent-olive"
                        />

                        <span className="flex min-w-0 flex-1 flex-col">
                            <span className="font-sans text-sm text-ink">
                                {opcao.transportadora
                                    ? `${opcao.transportadora} · ${opcao.servico}`
                                    : opcao.servico}
                            </span>
                            <span className="mt-0.5 text-xs text-ink-soft">
                                {prazoEmTexto(opcao.prazoDias)}
                            </span>
                        </span>

                        <span className="shrink-0 text-right">
                            {opcao.gratis ? (
                                <>
                                    <span className="font-sans text-xs uppercase tracking-widest text-olive">
                                        Cortesia
                                    </span>
                                    {teveDesconto && (
                                        <span className="preco block text-xs text-taupe line-through">
                                            {formatarCentavosParaBRL(opcao.valorCotadoCentavos)}
                                        </span>
                                    )}
                                </>
                            ) : (
                                <span className="preco text-sm text-ink">
                                    {formatarCentavosParaBRL(opcao.valorCentavos)}
                                </span>
                            )}
                        </span>
                    </label>
                );
            })}
        </fieldset>
    );
}
