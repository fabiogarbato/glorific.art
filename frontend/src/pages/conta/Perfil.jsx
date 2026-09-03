import { useEffect, useState } from "react";

import Botao from "@/components/ui/Botao.jsx";
import Campo from "@/components/ui/Campo.jsx";
import Skeleton from "@/components/ui/Skeleton.jsx";
import { LayoutConta } from "@/components/compra/NavConta.jsx";
import { usePerfil, useAtualizarPerfil } from "@/hooks/useConta.js";
import { useAuth } from "@/hooks/useAuth.js";
import { useToast } from "@/hooks/useToast.js";
import { paraInputDate } from "@/utils/datas.js";
import {
    CPF_MAXLENGTH,
    TELEFONE_MAXLENGTH,
    formatCPF,
    formatTelefone,
    isValidCPF,
    isValidTelefone,
    onlyDigits,
} from "@/utils/masks.js";

/**
 * Dados pessoais do cliente.
 *
 * O e-mail aparece travado de propósito: trocá-lo exige reverificação e tem
 * fluxo próprio no backend — não é um campo escondido num PUT de perfil.
 *
 * CPF e telefone são opcionais aqui (o backend aceita nulo), mas quando
 * preenchidos precisam estar corretos: o CPF vira documento de etiqueta na hora
 * da entrega.
 */
export default function Perfil() {
    const { perfil, isLoading, isError, refetch } = usePerfil();
    const atualizar = useAtualizarPerfil();
    const { recarregarPerfil } = useAuth();
    const toast = useToast();

    const [dados, setDados] = useState(null);
    const [erros, setErros] = useState({});

    // O formulário só nasce depois que o perfil chega — evita input controlado
    // trocando de indefinido para definido no meio da digitação.
    useEffect(() => {
        if (!perfil) return;
        setDados({
            nomeCompleto: perfil.nomeCompleto ?? "",
            telefone: formatTelefone(perfil.telefone ?? ""),
            cpf: formatCPF(perfil.cpf ?? ""),
            dataNascimento: paraInputDate(perfil.dataNascimento),
            aceitaMarketing: !!perfil.aceitaMarketing,
        });
    }, [perfil]);

    function alterar(campo, valor) {
        setDados((atual) => ({ ...atual, [campo]: valor }));
        setErros((atual) => (atual[campo] ? { ...atual, [campo]: undefined } : atual));
    }

    async function submeter(evento) {
        evento.preventDefault();

        const achados = {};
        if (dados.nomeCompleto.trim().length < 2) {
            achados.nomeCompleto = "Informe seu nome completo.";
        }
        if (dados.cpf && !isValidCPF(dados.cpf)) {
            achados.cpf = "CPF inválido. Confira os dígitos.";
        }
        if (dados.telefone && !isValidTelefone(dados.telefone)) {
            achados.telefone = "Telefone inválido. Use DDD e número.";
        }

        setErros(achados);
        if (Object.keys(achados).length > 0) return;

        try {
            await atualizar.mutateAsync({
                nomeCompleto: dados.nomeCompleto,
                telefone: onlyDigits(dados.telefone),
                cpf: onlyDigits(dados.cpf),
                dataNascimento: dados.dataNascimento || null,
                aceitaMarketing: dados.aceitaMarketing,
            });

            // A sessão guarda uma cópia do perfil (é dela que sai o nome no
            // cabeçalho). Sem este passo a pessoa salva e continua vendo o nome
            // antigo no topo da página.
            await recarregarPerfil().catch(() => {});

            toast.success("Perfil atualizado.");
        } catch {
            // O interceptor já mostrou o erro em toast.
        }
    }

    return (
        <LayoutConta
            titulo="Perfil"
            descricao="Os dados que usamos para falar com você e para emitir a nota do pedido."
        >
            {isLoading && (
                <div className="flex max-w-xl flex-col gap-6" aria-busy="true">
                    <Skeleton className="h-12 w-full" />
                    <Skeleton className="h-12 w-full" />
                    <Skeleton className="h-12 w-2/3" />
                </div>
            )}

            {isError && (
                <div className="max-w-xl">
                    <p className="text-base text-ink">
                        Não conseguimos carregar seu perfil agora.
                    </p>
                    <Botao variante="contorno" className="mt-6" onClick={() => refetch()}>
                        Tentar de novo
                    </Botao>
                </div>
            )}

            {!isLoading && !isError && dados && (
                <form onSubmit={submeter} noValidate className="flex max-w-xl flex-col gap-6">
                    <Campo
                        id="perfil-email"
                        label="E-mail"
                        type="email"
                        value={perfil.email}
                        readOnly
                        disabled
                        ajuda="Para trocar o e-mail, fale com a gente: a mudança precisa ser confirmada."
                    />

                    <Campo
                        id="perfil-nome"
                        label="Nome completo"
                        obrigatorio
                        maxLength={180}
                        autoComplete="name"
                        value={dados.nomeCompleto}
                        onChange={(e) => alterar("nomeCompleto", e.target.value)}
                        erro={erros.nomeCompleto}
                    />

                    <div className="grid gap-6 sm:grid-cols-2">
                        <Campo
                            id="perfil-cpf"
                            label="CPF"
                            inputMode="numeric"
                            maxLength={CPF_MAXLENGTH}
                            placeholder="000.000.000-00"
                            value={dados.cpf}
                            onChange={(e) => alterar("cpf", formatCPF(e.target.value))}
                            erro={erros.cpf}
                        />

                        <Campo
                            id="perfil-telefone"
                            label="Telefone"
                            inputMode="tel"
                            autoComplete="tel"
                            maxLength={TELEFONE_MAXLENGTH}
                            placeholder="(00) 00000-0000"
                            value={dados.telefone}
                            onChange={(e) => alterar("telefone", formatTelefone(e.target.value))}
                            erro={erros.telefone}
                        />
                    </div>

                    <Campo
                        id="perfil-nascimento"
                        label="Data de nascimento"
                        type="date"
                        containerClassName="sm:max-w-[16rem]"
                        value={dados.dataNascimento}
                        onChange={(e) => alterar("dataNascimento", e.target.value)}
                    />

                    <label className="flex items-start gap-3 text-sm leading-relaxed text-ink">
                        <input
                            type="checkbox"
                            checked={dados.aceitaMarketing}
                            onChange={(e) => alterar("aceitaMarketing", e.target.checked)}
                            className="mt-1 h-4 w-4 shrink-0 accent-olive"
                        />
                        Quero receber novidades da marca por e-mail. Escrevemos pouco e nunca
                        repassamos seu contato.
                    </label>

                    <div className="flex justify-end pt-2">
                        <Botao type="submit" carregando={atualizar.isPending}>
                            Salvar alterações
                        </Botao>
                    </div>
                </form>
            )}
        </LayoutConta>
    );
}
