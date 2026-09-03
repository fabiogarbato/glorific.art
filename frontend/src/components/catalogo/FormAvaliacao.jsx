import { useState } from "react";
import Botao from "@/components/ui/Botao.jsx";
import Campo from "@/components/ui/Campo.jsx";
import { useAuth } from "@/hooks/useAuth.js";
import { useCriarAvaliacao } from "@/hooks/useAvaliacoes.js";
import { getApiError } from "@/utils/apiError.js";
import { OPCOES_CAIMENTO } from "@/lib/vitrine.js";

/**
 * Formulario de avaliacao.
 *
 * Quem pode avaliar e quem COMPROU — e essa checagem e do backend, que amarra a
 * avaliacao a um item de pedido do proprio usuario. O front nao finge saber:
 * mostra o formulario para quem esta logado e exibe, sem rodeio, a resposta do
 * servidor quando a compra nao existe.
 *
 * Os campos de caimento sao opcionais de proposito. Exigi-los derruba a taxa de
 * envio, e uma avaliacao sem altura ainda vale — sem nota nenhuma, nao vale.
 */
const NOTAS = [1, 2, 3, 4, 5];

const ESTADO_INICIAL = {
    nota: 0,
    resumo: "",
    comentario: "",
    tamanhoComprado: "",
    alturaClienteCm: "",
    pesoClienteKg: "",
    caimento: "",
    recomenda: "",
};

export default function FormAvaliacao({ idProduto, tamanhos = [] }) {
    const { estaAutenticado } = useAuth();
    const { enviar, enviando, enviada, erro } = useCriarAvaliacao(idProduto);

    const [aberto, setAberto] = useState(false);
    const [form, setForm] = useState(ESTADO_INICIAL);
    const [erroNota, setErroNota] = useState(null);

    const alterar = (campo) => (evento) =>
        setForm((atual) => ({ ...atual, [campo]: evento.target.value }));

    async function enviarAvaliacao(evento) {
        evento.preventDefault();

        if (!form.nota) {
            setErroNota("Escolha uma nota de 1 a 5 estrelas.");
            return;
        }
        setErroNota(null);

        try {
            await enviar({
                nota: form.nota,
                titulo: form.resumo,
                comentario: form.comentario,
                tamanhoComprado: form.tamanhoComprado,
                alturaClienteCm: form.alturaClienteCm
                    ? Number.parseInt(form.alturaClienteCm, 10)
                    : null,
                pesoClienteKg: form.pesoClienteKg
                    ? Number(String(form.pesoClienteKg).replace(",", "."))
                    : null,
                caimento: form.caimento ? Number(form.caimento) : null,
                recomenda: form.recomenda === "" ? null : form.recomenda === "1",
            });
            setForm(ESTADO_INICIAL);
        } catch {
            // A mensagem do servidor aparece abaixo; o interceptor ja avisou por toast.
        }
    }

    if (!estaAutenticado) {
        return (
            <div className="border border-sand bg-linen p-6">
                <p className="text-sm leading-relaxed text-ink-soft">
                    Comprou esta peça? Entre na sua conta para contar como ela veste — é o que
                    mais ajuda quem está em dúvida entre dois tamanhos.
                </p>
                <Botao to="/login" variante="contorno" tamanho="sm" className="mt-4">
                    Entrar para avaliar
                </Botao>
            </div>
        );
    }

    if (enviada) {
        return (
            <div className="border border-sand bg-linen p-6">
                <p className="font-display text-xl tracking-tight text-ink">
                    Recebemos a sua avaliação.
                </p>
                <p className="mt-3 text-sm leading-relaxed text-ink-soft">
                    Ela passa por uma leitura da nossa equipe antes de aparecer nesta página.
                    Obrigado por ajudar quem vem depois.
                </p>
            </div>
        );
    }

    if (!aberto) {
        return (
            <Botao variante="contorno" onClick={() => setAberto(true)}>
                Escrever avaliação
            </Botao>
        );
    }

    const mensagemErro = erro ? getApiError(erro).message : null;

    return (
        <form onSubmit={enviarAvaliacao} className="border border-sand p-6" noValidate>
            <fieldset>
                <legend className="eyebrow">Sua nota</legend>
                <div className="mt-3 flex flex-wrap gap-2">
                    {NOTAS.map((nota) => (
                        <label
                            key={nota}
                            className={`inline-flex h-11 w-11 cursor-pointer items-center justify-center border text-sm transition-colors ${
                                form.nota === nota
                                    ? "border-ink bg-ink text-bone"
                                    : "border-sand bg-base-100 text-ink hover:border-ink"
                            }`}
                        >
                            <input
                                type="radio"
                                name="nota"
                                value={nota}
                                className="sr-only"
                                checked={form.nota === nota}
                                onChange={() => {
                                    setForm((atual) => ({ ...atual, nota }));
                                    setErroNota(null);
                                }}
                            />
                            {nota} ★
                        </label>
                    ))}
                </div>
                {erroNota && (
                    <p role="alert" className="mt-2 text-xs text-danger">
                        {erroNota}
                    </p>
                )}
            </fieldset>

            <div className="mt-6 flex flex-col gap-5">
                <Campo
                    label="Título"
                    maxLength={120}
                    value={form.resumo}
                    onChange={alterar("resumo")}
                    placeholder="Em poucas palavras"
                />

                <Campo
                    label="Seu relato"
                    como="textarea"
                    maxLength={4000}
                    value={form.comentario}
                    onChange={alterar("comentario")}
                    ajuda="Conte como a peça veste, o tecido e o caimento no seu corpo."
                />

                <div className="grid gap-5 sm:grid-cols-3">
                    <Campo
                        label="Tamanho que comprou"
                        como="select"
                        value={form.tamanhoComprado}
                        onChange={alterar("tamanhoComprado")}
                    >
                        <option value="">Prefiro não dizer</option>
                        {tamanhos.map((tamanho) => (
                            <option key={tamanho.id} value={tamanho.codigo}>
                                {tamanho.codigo}
                            </option>
                        ))}
                    </Campo>

                    <Campo
                        label="Sua altura (cm)"
                        type="number"
                        min={80}
                        max={250}
                        inputMode="numeric"
                        value={form.alturaClienteCm}
                        onChange={alterar("alturaClienteCm")}
                        placeholder="165"
                    />

                    <Campo
                        label="Seu peso (kg)"
                        inputMode="decimal"
                        value={form.pesoClienteKg}
                        onChange={alterar("pesoClienteKg")}
                        placeholder="62"
                    />
                </div>

                <div className="grid gap-5 sm:grid-cols-2">
                    <Campo
                        label="Como vestiu"
                        como="select"
                        value={form.caimento}
                        onChange={alterar("caimento")}
                    >
                        <option value="">Prefiro não dizer</option>
                        {OPCOES_CAIMENTO.map((opcao) => (
                            <option key={opcao.valor} value={opcao.valor}>
                                {opcao.rotulo}
                            </option>
                        ))}
                    </Campo>

                    <Campo
                        label="Recomenda esta peça?"
                        como="select"
                        value={form.recomenda}
                        onChange={alterar("recomenda")}
                    >
                        <option value="">Prefiro não dizer</option>
                        <option value="1">Sim, recomendo</option>
                        <option value="0">Não recomendo</option>
                    </Campo>
                </div>
            </div>

            {mensagemErro && (
                <p role="alert" className="mt-5 text-sm text-danger">
                    {mensagemErro}
                </p>
            )}

            <p className="mt-5 text-xs leading-relaxed text-ink-soft">
                Publicamos o primeiro nome e a inicial do sobrenome. Seu e-mail nunca aparece.
                A avaliação passa por moderação antes de entrar na página.
            </p>

            <div className="mt-5 flex flex-wrap gap-3">
                <Botao type="submit" carregando={enviando}>
                    Enviar avaliação
                </Botao>
                <Botao variante="texto" onClick={() => setAberto(false)} disabled={enviando}>
                    Cancelar
                </Botao>
            </div>
        </form>
    );
}
