import { useEffect, useRef, useState } from "react";

import Campo from "@/components/ui/Campo.jsx";
import Botao from "@/components/ui/Botao.jsx";
import { useCep } from "@/hooks/useCep.js";
import {
    CEP_MAXLENGTH,
    CPF_MAXLENGTH,
    TELEFONE_MAXLENGTH,
    formatCEP,
    formatCPF,
    formatTelefone,
    isValidCEP,
    isValidCPF,
    isValidTelefone,
    onlyDigits,
} from "@/utils/masks.js";

/** Unidades da federação — o backend guarda 2 letras maiúsculas. */
const UFS = [
    "AC", "AL", "AM", "AP", "BA", "CE", "DF", "ES", "GO", "MA", "MG", "MS",
    "MT", "PA", "PB", "PE", "PI", "PR", "RJ", "RN", "RO", "RR", "RS", "SC",
    "SE", "SP", "TO",
];

const VAZIO = {
    apelido: "",
    destinatario: "",
    documentoDestinatario: "",
    telefoneContato: "",
    cep: "",
    logradouro: "",
    numero: "",
    complemento: "",
    bairro: "",
    cidade: "",
    uf: "",
    principal: false,
};

/**
 * Validação de borda. Não substitui a do servidor — existe para que a pessoa
 * descubra o erro antes de perder o formulário num 400.
 *
 * O CPF é conferido pelos dois dígitos verificadores porque o backend RECUSA o
 * checkout com documento inválido (a transportadora exige documento para emitir
 * a etiqueta): descobrir isso só na hora de pagar seria cruel.
 *
 * O bairro é obrigatório porque o Melhor Envio exige `district` na criação do
 * envio — e por isso ele continua EDITÁVEL mesmo depois da busca por CEP, que
 * às vezes devolve o campo vazio em capitais.
 */
export function validarEndereco(dados) {
    const erros = {};

    if (dados.destinatario.trim().length < 2) {
        erros.destinatario = "Informe o nome de quem vai receber.";
    }

    if (!isValidCPF(dados.documentoDestinatario)) {
        erros.documentoDestinatario = "CPF inválido. Confira os dígitos.";
    }

    if (!isValidTelefone(dados.telefoneContato)) {
        erros.telefoneContato = "Telefone inválido. Use DDD e número.";
    }

    if (!isValidCEP(dados.cep)) erros.cep = "O CEP precisa ter 8 dígitos.";
    if (!dados.logradouro.trim()) erros.logradouro = "Informe a rua ou avenida.";
    if (!dados.numero.trim()) erros.numero = "Informe o número (ou escreva S/N).";
    if (!dados.bairro.trim()) erros.bairro = "Informe o bairro.";
    if (!dados.cidade.trim()) erros.cidade = "Informe a cidade.";
    if (!UFS.includes(dados.uf)) erros.uf = "Escolha a UF.";

    return erros;
}

export default function FormEndereco({
    valorInicial = null,
    onSubmit,
    onCancelar,
    salvando = false,
    mostrarPrincipal = true,
    textoConfirmar = "Salvar endereço",
}) {
    const [dados, setDados] = useState(() => ({
        ...VAZIO,
        ...(valorInicial ?? {}),
        cep: formatCEP(valorInicial?.cep ?? ""),
        documentoDestinatario: formatCPF(valorInicial?.documentoDestinatario ?? ""),
        telefoneContato: formatTelefone(valorInicial?.telefoneContato ?? ""),
        principal: !!valorInicial?.principal,
    }));

    const [erros, setErros] = useState({});
    const { buscar, buscando, naoEncontrado } = useCep();

    const refNumero = useRef(null);
    // Evita rebuscar o mesmo CEP a cada re-render do formulário.
    const ultimoCepBuscado = useRef(onlyDigits(valorInicial?.cep ?? ""));

    function alterar(campo, valor) {
        setDados((atual) => ({ ...atual, [campo]: valor }));
        setErros((atual) => (atual[campo] ? { ...atual, [campo]: undefined } : atual));
    }

    /** Busca assim que o CEP fica completo — sem exigir um clique a mais. */
    useEffect(() => {
        const digitos = onlyDigits(dados.cep);

        if (digitos.length !== 8 || digitos === ultimoCepBuscado.current) return;
        ultimoCepBuscado.current = digitos;

        let ativo = true;

        buscar(digitos).then((encontrado) => {
            if (!ativo || !encontrado) return;

            setDados((atual) => ({
                ...atual,
                // Só preenche o que veio: um bairro vazio do ViaCEP não pode
                // apagar o que a pessoa já tinha digitado.
                logradouro: encontrado.logradouro || atual.logradouro,
                bairro: encontrado.bairro || atual.bairro,
                cidade: encontrado.cidade || atual.cidade,
                uf: encontrado.uf || atual.uf,
            }));

            setErros((atual) => ({
                ...atual,
                logradouro: undefined,
                cidade: undefined,
                uf: undefined,
            }));

            // O que sobra para digitar depois do CEP é sempre o número.
            refNumero.current?.focus();
        });

        return () => {
            ativo = false;
        };
    }, [dados.cep, buscar]);

    function submeter(evento) {
        evento.preventDefault();

        const achados = validarEndereco(dados);
        setErros(achados);

        if (Object.keys(achados).length > 0) {
            // Leva o foco para o primeiro campo com problema.
            const primeiro = document.getElementById(`entrega-${Object.keys(achados)[0]}`);
            primeiro?.focus();
            return;
        }

        onSubmit({
            ...dados,
            cep: onlyDigits(dados.cep),
            documentoDestinatario: onlyDigits(dados.documentoDestinatario),
            telefoneContato: onlyDigits(dados.telefoneContato),
        });
    }

    return (
        <form onSubmit={submeter} noValidate className="flex flex-col gap-5">
            <div className="grid gap-5 sm:grid-cols-2">
                <Campo
                    id="entrega-destinatario"
                    label="Quem vai receber"
                    obrigatorio
                    maxLength={180}
                    autoComplete="name"
                    value={dados.destinatario}
                    onChange={(e) => alterar("destinatario", e.target.value)}
                    erro={erros.destinatario}
                />

                <Campo
                    id="entrega-documentoDestinatario"
                    label="CPF de quem recebe"
                    obrigatorio
                    inputMode="numeric"
                    maxLength={CPF_MAXLENGTH}
                    placeholder="000.000.000-00"
                    value={dados.documentoDestinatario}
                    onChange={(e) => alterar("documentoDestinatario", formatCPF(e.target.value))}
                    erro={erros.documentoDestinatario}
                    ajuda="A transportadora exige o documento para emitir a etiqueta."
                />

                <Campo
                    id="entrega-telefoneContato"
                    label="Telefone de contato"
                    obrigatorio
                    inputMode="tel"
                    autoComplete="tel"
                    maxLength={TELEFONE_MAXLENGTH}
                    placeholder="(00) 00000-0000"
                    value={dados.telefoneContato}
                    onChange={(e) => alterar("telefoneContato", formatTelefone(e.target.value))}
                    erro={erros.telefoneContato}
                />

                <Campo
                    id="entrega-cep"
                    label="CEP"
                    obrigatorio
                    inputMode="numeric"
                    autoComplete="postal-code"
                    maxLength={CEP_MAXLENGTH}
                    placeholder="00000-000"
                    value={dados.cep}
                    onChange={(e) => alterar("cep", formatCEP(e.target.value))}
                    erro={erros.cep}
                    ajuda={
                        buscando
                            ? "Buscando o endereço…"
                            : naoEncontrado
                              ? "Não encontramos esse CEP. Preencha os campos abaixo à mão."
                              : undefined
                    }
                />
            </div>

            <div className="grid gap-5 sm:grid-cols-[2fr_1fr]">
                <Campo
                    id="entrega-logradouro"
                    label="Rua ou avenida"
                    obrigatorio
                    maxLength={200}
                    autoComplete="address-line1"
                    value={dados.logradouro}
                    onChange={(e) => alterar("logradouro", e.target.value)}
                    erro={erros.logradouro}
                />

                <Campo
                    id="entrega-numero"
                    name="numero"
                    ref={refNumero}
                    label="Número"
                    obrigatorio
                    maxLength={20}
                    value={dados.numero}
                    // A chave vem do próprio `name` do campo: além de casar com
                    // o autofill do navegador, evita repetir o nome do campo.
                    onChange={(e) => alterar(e.target.name, e.target.value)}
                    erro={erros.numero}
                />
            </div>

            <div className="grid gap-5 sm:grid-cols-2">
                <Campo
                    id="entrega-complemento"
                    label="Complemento"
                    maxLength={120}
                    autoComplete="address-line2"
                    placeholder="Apartamento, bloco, referência"
                    value={dados.complemento}
                    onChange={(e) => alterar("complemento", e.target.value)}
                />

                <Campo
                    id="entrega-bairro"
                    label="Bairro"
                    obrigatorio
                    maxLength={120}
                    value={dados.bairro}
                    onChange={(e) => alterar("bairro", e.target.value)}
                    erro={erros.bairro}
                    ajuda="Confira: a transportadora recusa envio sem bairro."
                />
            </div>

            <div className="grid gap-5 sm:grid-cols-[2fr_1fr]">
                <Campo
                    id="entrega-cidade"
                    label="Cidade"
                    obrigatorio
                    maxLength={120}
                    value={dados.cidade}
                    onChange={(e) => alterar("cidade", e.target.value)}
                    erro={erros.cidade}
                />

                <Campo
                    id="entrega-uf"
                    como="select"
                    label="UF"
                    obrigatorio
                    value={dados.uf}
                    onChange={(e) => alterar("uf", e.target.value)}
                    erro={erros.uf}
                >
                    <option value="">Selecione</option>
                    {UFS.map((uf) => (
                        <option key={uf} value={uf}>
                            {uf}
                        </option>
                    ))}
                </Campo>
            </div>

            <Campo
                id="entrega-apelido"
                label="Apelido deste endereço"
                maxLength={60}
                placeholder="Casa, trabalho…"
                value={dados.apelido ?? ""}
                onChange={(e) => alterar("apelido", e.target.value)}
            />

            {mostrarPrincipal && (
                <label className="flex items-center gap-3 text-sm text-ink">
                    <input
                        type="checkbox"
                        checked={dados.principal}
                        onChange={(e) => alterar("principal", e.target.checked)}
                        className="h-4 w-4 accent-olive"
                    />
                    Usar como endereço principal
                </label>
            )}

            <div className="flex flex-wrap justify-end gap-3 pt-2">
                {onCancelar && (
                    <Botao variante="contorno" onClick={onCancelar} disabled={salvando}>
                        Cancelar
                    </Botao>
                )}
                <Botao type="submit" carregando={salvando}>
                    {textoConfirmar}
                </Botao>
            </div>
        </form>
    );
}
