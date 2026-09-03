import { formatarDataHora, paraData } from "@/utils/datas.js";
import { descreverStatusEnvio, descreverStatusPedido } from "./StatusPedidoBadge.jsx";

/**
 * Linha do tempo do pedido.
 *
 * Junta duas fontes que o backend expõe separadas: o histórico de status do
 * pedido (`historico`) e os eventos de rastreio da transportadora (`eventos`).
 * Ficam na mesma coluna porque, para quem comprou, é uma história só — e sai do
 * mais recente para o mais antigo, que é a ordem em que a pergunta aparece
 * ("cadê meu pedido agora?").
 *
 * O rótulo cru do enum nunca chega à tela: passa pelos mapas de status.
 */
function montarEtapas({ historico = [], eventos = [] }) {
    const doPedido = historico.map((h) => ({
        chave: `pedido-${h.dataAlteracao}-${h.statusNovo}`,
        rotulo: descreverStatusPedido(h.statusNovo).rotulo,
        detalhe: h.observacao ?? null,
        quando: h.dataAlteracao,
    }));

    const doEnvio = eventos.map((e, i) => ({
        chave: `envio-${e.ocorridoEm}-${i}`,
        rotulo: e.descricao?.trim() || descreverStatusEnvio(e.status).rotulo,
        detalhe: e.local ?? null,
        quando: e.ocorridoEm,
    }));

    return [...doPedido, ...doEnvio].sort((a, b) => {
        const da = paraData(a.quando)?.getTime() ?? 0;
        const db = paraData(b.quando)?.getTime() ?? 0;
        return db - da;
    });
}

export default function LinhaDoTempoPedido({
    historico = [],
    eventos = [],
    titulo = "Acompanhamento",
}) {
    const etapas = montarEtapas({ historico, eventos });

    if (etapas.length === 0) {
        return (
            <section aria-label={titulo} className="flex flex-col gap-3">
                <h2 className="font-display text-xl tracking-tight text-ink">{titulo}</h2>
                <p className="text-sm leading-relaxed text-ink-soft">
                    Ainda não há movimentações registradas. Assim que o pedido avançar, o
                    histórico aparece aqui.
                </p>
            </section>
        );
    }

    return (
        <section aria-label={titulo} className="flex flex-col gap-5">
            <h2 className="font-display text-xl tracking-tight text-ink">{titulo}</h2>

            <ol className="flex flex-col">
                {etapas.map((etapa, indice) => {
                    const atual = indice === 0;

                    return (
                        <li key={etapa.chave} className="flex gap-4">
                            {/* Trilho: marcador cheio na etapa mais recente. */}
                            <div
                                className="flex w-3 shrink-0 flex-col items-center"
                                aria-hidden="true"
                            >
                                <span
                                    className={`mt-1.5 h-2.5 w-2.5 shrink-0 rounded-full border ${
                                        atual ? "border-olive bg-olive" : "border-sand bg-base-100"
                                    }`}
                                />
                                {indice < etapas.length - 1 && (
                                    <span className="w-px flex-1 bg-sand" />
                                )}
                            </div>

                            <div className={`min-w-0 pb-6 ${atual ? "" : "opacity-80"}`}>
                                <p
                                    className={`font-sans text-sm ${atual ? "text-ink" : "text-ink-soft"}`}
                                >
                                    {etapa.rotulo}
                                </p>

                                {etapa.detalhe && (
                                    <p className="mt-0.5 text-sm text-ink-soft">{etapa.detalhe}</p>
                                )}

                                <time
                                    dateTime={etapa.quando}
                                    className="mt-1 block font-sans text-xs uppercase tracking-widest text-taupe"
                                >
                                    {formatarDataHora(etapa.quando)}
                                </time>
                            </div>
                        </li>
                    );
                })}
            </ol>
        </section>
    );
}
