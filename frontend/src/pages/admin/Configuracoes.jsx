import { useEffect, useState } from "react";
import { FiAlertTriangle } from "react-icons/fi";

import {
    CabecalhoPagina,
    EstadoErro,
} from "@/components/admin/EstadoConsulta.jsx";
import Botao from "@/components/ui/Botao.jsx";
import Campo from "@/components/ui/Campo.jsx";
import Skeleton from "@/components/ui/Skeleton.jsx";

import { useConfiguracao, useSalvarConfiguracao } from "@/hooks/useConfiguracao.js";
import { formatCEP, isValidCEP, onlyDigits } from "@/utils/masks.js";
import { formatarDataHora } from "@/utils/datas.js";
import { mascaraPrecoCentavos, parseBRLParaCentavos } from "@/utils/financeiro.js";

/**
 * Configuração operacional da loja (policy SomenteAdmin).
 *
 * Não é decisão de catálogo, é decisão de operação, e vale para a loja inteira
 * no ato: o CEP de origem muda toda cotação de frete, o prazo de manuseio
 * empurra todo prazo exibido na vitrine e o pedido mínimo barra o checkout.
 * Por isso cada campo aqui vem com a consequência escrita ao lado.
 */

const VAZIO = {
    freteGratisAcimaDe: "",
    prazoManuseioDias: "2",
    cepOrigem: "",
    politicaTrocaDias: "7",
    pedidoMinimo: "",
    exibirEstoqueBaixo: false,
    limiteEstoqueBaixo: "3",
};

function paraFormulario(c) {
    if (!c) return VAZIO;
    return {
        freteGratisAcimaDe: c.freteGratisAcimaDeCentavos
            ? mascaraPrecoCentavos(String(c.freteGratisAcimaDeCentavos))
            : "",
        prazoManuseioDias: String(c.prazoManuseioDias ?? 2),
        cepOrigem: formatCEP(c.cepOrigem ?? ""),
        politicaTrocaDias: String(c.politicaTrocaDias ?? 7),
        pedidoMinimo: c.pedidoMinimoCentavos
            ? mascaraPrecoCentavos(String(c.pedidoMinimoCentavos))
            : "",
        exibirEstoqueBaixo: !!c.exibirEstoqueBaixo,
        limiteEstoqueBaixo: String(c.limiteEstoqueBaixo ?? 3),
    };
}

export default function Configuracoes() {
    const { configuracao, isLoading, isError, refetch } = useConfiguracao();
    const salvar = useSalvarConfiguracao();

    const [formulario, setFormulario] = useState(VAZIO);
    const [erros, setErros] = useState({});

    // Só reidrata quando a API responde: escrever a cada render apagaria o que
    // o admin está digitando enquanto uma revalidação em segundo plano volta.
    useEffect(() => {
        if (configuracao) setFormulario(paraFormulario(configuracao));
    }, [configuracao]);

    const trocar = (campo) => (e) =>
        setFormulario((atual) => ({ ...atual, [campo]: e.target.value }));

    const validar = () => {
        const e = {};
        if (!isValidCEP(formulario.cepOrigem)) e.cepOrigem = "Informe um CEP com 8 dígitos.";

        const manuseio = Number(formulario.prazoManuseioDias);
        if (!Number.isInteger(manuseio) || manuseio < 0 || manuseio > 60) {
            e.prazoManuseioDias = "Entre 0 e 60 dias.";
        }

        const troca = Number(formulario.politicaTrocaDias);
        if (!Number.isInteger(troca) || troca < 0 || troca > 365) {
            e.politicaTrocaDias = "Entre 0 e 365 dias.";
        }

        const limite = Number(formulario.limiteEstoqueBaixo);
        if (!Number.isInteger(limite) || limite < 1 || limite > 999) {
            e.limiteEstoqueBaixo = "Entre 1 e 999 peças.";
        }

        return e;
    };

    const enviar = (evento) => {
        evento.preventDefault();
        const e = validar();
        setErros(e);
        if (Object.keys(e).length > 0) return;

        salvar.mutate({
            freteGratisAcimaDeCentavos: parseBRLParaCentavos(formulario.freteGratisAcimaDe) || null,
            prazoManuseioDias: Number(formulario.prazoManuseioDias),
            cepOrigem: onlyDigits(formulario.cepOrigem),
            politicaTrocaDias: Number(formulario.politicaTrocaDias),
            pedidoMinimoCentavos: parseBRLParaCentavos(formulario.pedidoMinimo) || null,
            exibirEstoqueBaixo: formulario.exibirEstoqueBaixo,
            limiteEstoqueBaixo: Number(formulario.limiteEstoqueBaixo),
        });
    };

    if (isError) {
        return (
            <div className="animate-fade-up">
                <CabecalhoPagina sobretitulo="Configuração" titulo="Configurações da loja" />
                <EstadoErro
                    mensagem="As configurações não puderam ser carregadas."
                    onTentarDeNovo={refetch}
                />
            </div>
        );
    }

    return (
        <div className="animate-fade-up">
            <CabecalhoPagina
                sobretitulo="Configuração"
                titulo="Configurações da loja"
                descricao="Tudo aqui vale para a loja inteira e passa a valer na próxima cotação de frete, não daqui a alguns minutos."
            />

            {isLoading ? (
                <div className="flex max-w-3xl flex-col gap-6">
                    {Array.from({ length: 5 }).map((_, i) => (
                        <Skeleton key={i} className="h-16 w-full" />
                    ))}
                </div>
            ) : (
                <form onSubmit={enviar} className="max-w-3xl">
                    <fieldset className="mb-10 border-0 p-0">
                        <legend className="eyebrow mb-4">Frete e prazo</legend>

                        <div className="grid gap-5 sm:grid-cols-2">
                            <Campo
                                label="CEP de origem"
                                obrigatorio
                                inputMode="numeric"
                                maxLength={9}
                                value={formulario.cepOrigem}
                                erro={erros.cepOrigem}
                                ajuda="De onde as peças saem. Muda toda cotação de frete da loja."
                                onChange={(e) =>
                                    setFormulario((atual) => ({
                                        ...atual,
                                        cepOrigem: formatCEP(e.target.value),
                                    }))
                                }
                            />

                            <Campo
                                label="Prazo de manuseio"
                                type="number"
                                min="0"
                                max="60"
                                obrigatorio
                                value={formulario.prazoManuseioDias}
                                erro={erros.prazoManuseioDias}
                                ajuda="Dias entre o pagamento e a postagem. Soma no prazo mostrado na vitrine."
                                onChange={trocar("prazoManuseioDias")}
                            />

                            <Campo
                                label="Frete grátis acima de"
                                inputMode="numeric"
                                value={formulario.freteGratisAcimaDe}
                                ajuda="Em reais. Deixe em branco para desligar a regra."
                                onChange={(e) =>
                                    setFormulario((atual) => ({
                                        ...atual,
                                        freteGratisAcimaDe: mascaraPrecoCentavos(e.target.value),
                                    }))
                                }
                            />

                            <Campo
                                label="Pedido mínimo"
                                inputMode="numeric"
                                value={formulario.pedidoMinimo}
                                ajuda="Abaixo deste valor o checkout é bloqueado. Em branco não exige mínimo."
                                onChange={(e) =>
                                    setFormulario((atual) => ({
                                        ...atual,
                                        pedidoMinimo: mascaraPrecoCentavos(e.target.value),
                                    }))
                                }
                            />
                        </div>
                    </fieldset>

                    <fieldset className="mb-10 border-0 p-0">
                        <legend className="eyebrow mb-4">Pós-venda</legend>

                        <Campo
                            label="Prazo de troca"
                            type="number"
                            min="0"
                            max="365"
                            obrigatorio
                            containerClassName="sm:max-w-xs"
                            value={formulario.politicaTrocaDias}
                            erro={erros.politicaTrocaDias}
                            ajuda="Dias após o recebimento em que a cliente pode pedir troca."
                            onChange={trocar("politicaTrocaDias")}
                        />
                    </fieldset>

                    <fieldset className="mb-10 border-0 p-0">
                        <legend className="eyebrow mb-4">Alerta de estoque</legend>

                        <label className="flex items-center gap-3 text-sm text-ink">
                            <input
                                type="checkbox"
                                className="h-4 w-4 accent-olive"
                                checked={formulario.exibirEstoqueBaixo}
                                onChange={(e) =>
                                    setFormulario((atual) => ({
                                        ...atual,
                                        exibirEstoqueBaixo: e.target.checked,
                                    }))
                                }
                            />
                            Mostrar aviso de últimas peças na vitrine
                        </label>

                        <Campo
                            label="Limite de estoque baixo"
                            type="number"
                            min="1"
                            max="999"
                            containerClassName="mt-5 sm:max-w-xs"
                            value={formulario.limiteEstoqueBaixo}
                            erro={erros.limiteEstoqueBaixo}
                            ajuda="Quantidade a partir da qual a peça é tratada como quase esgotada."
                            onChange={trocar("limiteEstoqueBaixo")}
                        />
                    </fieldset>

                    <div className="flex flex-wrap items-center gap-4 border-t border-sand pt-6">
                        <Botao type="submit" carregando={salvar.isPending}>
                            Salvar configurações
                        </Botao>

                        {configuracao?.dataAlteracao && (
                            <p className="text-xs text-taupe">
                                Última alteração em {formatarDataHora(configuracao.dataAlteracao)}.
                            </p>
                        )}
                    </div>

                    <p className="mt-6 flex items-start gap-2 text-xs leading-relaxed text-ink-soft">
                        <FiAlertTriangle size={14} className="mt-0.5 shrink-0 text-warning" aria-hidden="true" />
                        Um erro de digitação aqui é caro e silencioso: um prazo de manuseio alto
                        empurra o prazo de toda a loja e o sintoma só aparece dias depois, como
                        cliente reclamando de demora.
                    </p>
                </form>
            )}
        </div>
    );
}
