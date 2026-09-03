import { useState } from "react";
import Botao from "@/components/ui/Botao.jsx";
import { useFreteProduto } from "@/hooks/useFreteProduto.js";
import { formatarCentavosParaBRL } from "@/utils/financeiro.js";
import { CEP_MAXLENGTH, formatCEP, isValidCEP } from "@/utils/masks.js";

/**
 * Calculadora de frete da pagina de produto.
 *
 * Cota por VARIACAO porque peso e dimensao moram no SKU: "Vestido P" e
 * "Vestido GG" nao pesam a mesma coisa. Enquanto o tamanho nao esta escolhido,
 * a simulacao usa a primeira variacao com saldo e diz isso na tela — prometer
 * um valor sem avisar de qual peca ele saiu e o comeco de uma reclamacao.
 */
function mensagemDoErro(erro) {
    const status = erro?.response?.status;
    if (status === 429) {
        return "Muitas consultas seguidas. Aguarde alguns instantes e tente de novo.";
    }
    if (status === 502) {
        return "A transportadora não respondeu agora. Tente novamente em instantes.";
    }
    if (status === 400) {
        return erro?.response?.data?.error || "Confira o CEP e tente novamente.";
    }
    return "Não foi possível calcular o frete agora.";
}

export default function CalculadoraFrete({
    variacao = null,
    quantidade = 1,
    tamanhoEscolhido = false,
}) {
    const [cep, setCep] = useState("");
    const [tocado, setTocado] = useState(false);
    const { cotar, opcoes, cotando, cotou, semServico, erro, limpar } = useFreteProduto();

    const cepValido = isValidCEP(cep);
    const podeCotar = !!variacao && cepValido;

    async function calcular(evento) {
        evento.preventDefault();
        setTocado(true);
        if (!podeCotar) return;
        try {
            await cotar({
                cep,
                itens: [{ idVariacao: variacao.id, quantidade }],
            });
        } catch {
            // O estado de erro do hook ja alimenta a mensagem abaixo.
        }
    }

    if (!variacao) {
        return (
            <div className="border-t border-sand pt-6">
                <p className="eyebrow">Frete e prazo</p>
                <p className="mt-3 text-sm text-ink-soft">
                    O cálculo de frete volta assim que esta peça tiver estoque.
                </p>
            </div>
        );
    }

    return (
        <div className="border-t border-sand pt-6">
            <p className="eyebrow">Frete e prazo</p>

            <form onSubmit={calcular} className="mt-3 flex flex-wrap items-end gap-3" noValidate>
                <div className="min-w-[9rem] flex-1">
                    <label htmlFor="cep-frete" className="mb-1 block text-xs text-ink-soft">
                        CEP de entrega
                    </label>
                    <input
                        id="cep-frete"
                        name="cep"
                        inputMode="numeric"
                        autoComplete="postal-code"
                        maxLength={CEP_MAXLENGTH}
                        placeholder="00000-000"
                        value={cep}
                        aria-invalid={tocado && !cepValido ? true : undefined}
                        aria-describedby="cep-frete-ajuda"
                        onChange={(e) => {
                            setCep(formatCEP(e.target.value));
                            if (cotou || erro) limpar();
                        }}
                        className="w-full border border-sand bg-base-100 px-3 py-2.5 text-base tabular-nums text-ink placeholder:text-taupe focus:border-olive focus:outline-none"
                    />
                </div>

                <Botao type="submit" variante="contorno" carregando={cotando}>
                    Calcular
                </Botao>
            </form>

            <p id="cep-frete-ajuda" className="mt-2 text-xs text-ink-soft">
                {tocado && !cepValido ? (
                    <span className="text-danger">Informe os 8 dígitos do CEP.</span>
                ) : !tamanhoEscolhido ? (
                    <>Simulação para 1 peça no tamanho {variacao.codigoTamanho}.</>
                ) : (
                    <>
                        Valor para {quantidade}{" "}
                        {quantidade === 1 ? "peça" : "peças"} no tamanho{" "}
                        {variacao.codigoTamanho}.
                    </>
                )}
            </p>

            {erro && (
                <p role="alert" className="mt-4 text-sm text-danger">
                    {mensagemDoErro(erro)}
                </p>
            )}

            {cotou && semServico && (
                <p className="mt-4 text-sm text-ink-soft">
                    Nenhuma transportadora atende esse CEP no momento. Fale com a gente pelo
                    WhatsApp que a gente encontra um caminho.
                </p>
            )}

            {opcoes.length > 0 && (
                <ul className="mt-4 divide-y divide-sand border-t border-sand">
                    {opcoes.map((opcao) => (
                        <li
                            key={`${opcao.idServico}-${opcao.servico}`}
                            className="flex items-center justify-between gap-4 py-3"
                        >
                            <div className="min-w-0">
                                <p className="text-sm text-ink">
                                    {opcao.transportadora
                                        ? `${opcao.transportadora} · ${opcao.servico}`
                                        : opcao.servico}
                                </p>
                                <p className="mt-0.5 text-xs text-ink-soft">
                                    {opcao.prazoDias
                                        ? opcao.prazoDias === 1
                                            ? "Chega em 1 dia útil"
                                            : `Chega em até ${opcao.prazoDias} dias úteis`
                                        : "Prazo informado no checkout"}
                                </p>
                            </div>

                            <p className="preco shrink-0 text-right text-sm">
                                {opcao.gratis ? (
                                    <>
                                        {opcao.valorCotadoCentavos > 0 && (
                                            <span className="mr-2 text-xs text-taupe line-through">
                                                {formatarCentavosParaBRL(
                                                    opcao.valorCotadoCentavos,
                                                )}
                                            </span>
                                        )}
                                        <span className="uppercase tracking-widest text-olive">
                                            Cortesia
                                        </span>
                                    </>
                                ) : (
                                    <span className="text-ink">
                                        {formatarCentavosParaBRL(opcao.valorCentavos)}
                                    </span>
                                )}
                            </p>
                        </li>
                    ))}
                </ul>
            )}
        </div>
    );
}
