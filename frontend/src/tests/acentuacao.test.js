import fs from "node:fs";
import path from "node:path";
import { fileURLToPath } from "node:url";
import { describe, expect, it } from "vitest";

/**
 * Guarda de acentuacao da copy.
 *
 * Uma loja brasileira com portugues sem acento parece quebrada. Este teste varre
 * o texto que chega aos olhos do usuario (texto JSX, valores de atributo como
 * title/alt/aria-label/placeholder e strings de mensagem) em src.  e no
 * index.html, e falha apontando arquivo e linha quando encontra uma palavra
 * comum escrita sem acento.
 *
 * Fora do escopo, de proposito: comentarios de codigo, valores de className,
 * rotas/URLs e identificadores (nome de prop, chave de objeto, variavel).
 */

const AQUI = import.meta.dirname ?? path.dirname(fileURLToPath(import.meta.url));
const RAIZ = path.resolve(AQUI, "..", "..");

/** Formas SEM acento que nunca deveriam aparecer na copy. */
const PALAVRAS = [
    "nao",
    "sao",
    "voce",
    "endereco",
    "enderecos",
    "pedido nao",
    "colecao",
    "colecoes",
    "tamanho unico",
    "selecao",
    "producao",
    "pecas",
    "calca",
    "algodao",
    "portugues",
    "ingles",
    "mes",
    "tres",
    "ja",
    "crista",
    "sera",
    "alem",
    "apos",
    "ate",
    "obrigatorio",
    "disponivel",
    "usuario nao",
    "codigo",
    "numero",
    "minimo",
    "maximo",
    "padrao",
    "opcao",
    "opcoes",
    "botao",
    "titulo",
    "descricao",
    "categoria nao",
    "avaliacao",
    "avaliacoes",
    "confirmacao",
    "atencao",
    "informacao",
    "informacoes",
    "transacao",
    "situacao",
    "permissao",
    "sessao",
    "autenticacao",
    "autorizacao",
    "validacao",
    "integracao",
    "configuracao",
    "configuracoes",
    "manutencao",
    "devolucao",
    "promocao",
    "promocoes",
    "cupom nao",
    "historico",
    "proximo",
    "ultimo",
    "unico",
    "unica",
    "disponiveis",
    "indisponivel",
    "sucesso nao",
    // Alem do minimo pedido: as demais que aparecem na copy desta loja.
    "historia",
    "historias",
    "catalogo",
    "pagina",
    "paginas",
    "area",
    "areas",
    "acao",
    "acoes",
    "possivel",
    "valido",
    "invalido",
    "inicio",
    "conteudo",
    "operacao",
    "visao",
    "relatorio",
    "relatorios",
    "usuario",
    "usuarios",
];

/**
 * Fronteira de palavra propria: `\b` do JS quebra em caractere acentuado, o que
 * faria "pagina" casar dentro de "Paginação". Aqui letra acentuada e letra.
 */
const ANTES = "(?<![\\p{L}\\p{N}_])";
const DEPOIS = "(?![\\p{L}\\p{N}_])";

const REGRAS = PALAVRAS.map((palavra) => ({
    palavra,
    re: new RegExp(`${ANTES}${palavra.split(" ").join("\\s+")}${DEPOIS}`, "giu"),
}));

/** Caracteres que so aparecem em codigo — encerram um trecho de texto JSX. */
const FIM_DE_TEXTO = new Set(`<>{}()[];=&|"'\`/\\+*%$#@~^`.split(""));

/** Rota, ancora, URL, alias de import e caminho de arquivo nao sao copy. */
const EH_CAMINHO = /^(?:https?:|mailto:|tel:|data:|[#/.@])/;
const EH_MODULO = /\b(?:from|import|require\()\s*$/;
const EH_CLASSNAME = /(?:className|class)\s*=\s*\{?\s*$/;
/** Atributos que carregam identificador tecnico, nunca copy. */
const EH_ATRIBUTO_TECNICO =
    /\b(?:path|to|href|src|id|htmlFor|key|name|type|role|rel|target|autoComplete|variante|tamanho|chave)\s*=\s*\{?\s*$/;

/** Preserva o tamanho do trecho removido para a linha reportada continuar certa. */
const embranquecer = (trecho) => " ".repeat(trecho.length);

/**
 * Varredor de JS/JSX. Pula comentarios e devolve so o que e texto para o
 * usuario: conteudo de string literal e texto solto dentro do JSX.
 */
function fatiasDeCodigo(codigo) {
    const fatias = [];
    const n = codigo.length;
    let i = 0;

    const anteriorSignificativo = (pos) => {
        let k = pos - 1;
        while (k >= 0 && /\s/.test(codigo[k])) k -= 1;
        return k >= 0 ? codigo[k] : "";
    };

    while (i < n) {
        const c = codigo[i];

        if (c === "/" && codigo[i + 1] === "/") {
            const fim = codigo.indexOf("\n", i);
            i = fim === -1 ? n : fim;
            continue;
        }

        if (c === "/" && codigo[i + 1] === "*") {
            const fim = codigo.indexOf("*/", i + 2);
            i = fim === -1 ? n : fim + 2;
            continue;
        }

        if (c === '"' || c === "'" || c === "`") {
            let j = i + 1;
            while (j < n) {
                if (codigo[j] === "\\") {
                    j += 2;
                    continue;
                }
                if (codigo[j] === c) break;
                j += 1;
            }
            fatias.push({
                inicio: i + 1,
                // `${...}` e expressao, nao texto.
                texto: codigo.slice(i + 1, j).replace(/\$\{[^{}]*\}/g, embranquecer),
                antes: codigo.slice(Math.max(0, i - 60), i),
            });
            i = Math.min(j + 1, n);
            continue;
        }

        // Texto de JSX: o que vem depois de `>` ate o proximo caractere de codigo.
        // `=>`, `>=` e afins nao abrem texto.
        if (c === ">" && !["=", "!", "<", ">", "-"].includes(anteriorSignificativo(i))) {
            let j = i + 1;
            while (j < n && !FIM_DE_TEXTO.has(codigo[j])) j += 1;
            fatias.push({ inicio: i + 1, texto: codigo.slice(i + 1, j), antes: "" });
            i = j;
            continue;
        }

        i += 1;
    }

    return fatias;
}

/** Varredor de HTML: valor de atributo (menos os tecnicos) e no de texto. */
function fatiasDeHtml(html) {
    const limpo = html.replace(/<!--[\s\S]*?-->/g, embranquecer);
    const fatias = [];

    const tecnicos = /^(?:class|href|src|rel|type|charset|id|name|property|crossorigin)$/i;
    const atributo = /([a-zA-Z:-]+)\s*=\s*"([^"]*)"/g;
    let m;
    while ((m = atributo.exec(limpo)) !== null) {
        if (tecnicos.test(m[1])) continue;
        fatias.push({ inicio: m.index + m[0].indexOf('"') + 1, texto: m[2], antes: "" });
    }

    const noDeTexto = />([^<>]*)</g;
    while ((m = noDeTexto.exec(limpo)) !== null) {
        fatias.push({ inicio: m.index + 1, texto: m[1], antes: "" });
    }

    return fatias;
}

function contadorDeLinhas(conteudo) {
    const quebras = [];
    for (let i = 0; i < conteudo.length; i += 1) {
        if (conteudo[i] === "\n") quebras.push(i);
    }
    return (offset) => {
        let linha = 1;
        for (const q of quebras) {
            if (q >= offset) break;
            linha += 1;
        }
        return linha;
    };
}

function violacoesEm(arquivo, conteudo) {
    const ehHtml = arquivo.endsWith(".html");
    const fatias = ehHtml ? fatiasDeHtml(conteudo) : fatiasDeCodigo(conteudo);
    const linhaDe = contadorDeLinhas(conteudo);
    const achados = [];

    for (const fatia of fatias) {
        const texto = fatia.texto;
        if (!texto || !/[a-zA-Z]/.test(texto)) continue;
        if (EH_CAMINHO.test(texto.trim())) continue;
        if (
            fatia.antes &&
            (EH_CLASSNAME.test(fatia.antes) ||
                EH_MODULO.test(fatia.antes) ||
                EH_ATRIBUTO_TECNICO.test(fatia.antes))
        ) {
            continue;
        }

        for (const { palavra, re } of REGRAS) {
            re.lastIndex = 0;
            let m;
            while ((m = re.exec(texto)) !== null) {
                achados.push({
                    arquivo,
                    linha: linhaDe(fatia.inicio + m.index),
                    palavra,
                    trecho: texto.trim().replace(/\s+/g, " ").slice(0, 90),
                });
            }
        }
    }

    return achados;
}

function listarJsx(dir) {
    const saida = [];
    for (const entrada of fs.readdirSync(dir, { withFileTypes: true })) {
        const alvo = path.join(dir, entrada.name);
        if (entrada.isDirectory()) saida.push(...listarJsx(alvo));
        else if (entrada.name.endsWith(".jsx")) saida.push(alvo);
    }
    return saida;
}

const ARQUIVOS = [...listarJsx(path.join(RAIZ, "src")), path.join(RAIZ, "index.html")];

describe("acentuacao da copy", () => {
    it("encontra os arquivos de interface", () => {
        expect(ARQUIVOS.length).toBeGreaterThan(5);
    });

    it("nao deixa palavra portuguesa sem acento no texto visivel", () => {
        const relatorio = ARQUIVOS.flatMap((arquivo) =>
            violacoesEm(arquivo, fs.readFileSync(arquivo, "utf8")).map(
                (v) =>
                    `${path.relative(RAIZ, v.arquivo).replace(/\\/g, "/")}:${v.linha} ` +
                    `escreveu "${v.palavra}" sem acento -> ${v.trecho}`,
            ),
        );

        expect(relatorio).toEqual([]);
    });

    it("o varredor realmente acusa quando a copy perde o acento", () => {
        const quebrado = [
            'export default function X() {',
            '    return (',
            '        <p className="text-ink" title="Selecao do mes">',
            '            Colecao nao encontrada',
            "        </p>",
            "    );",
            "}",
        ].join("\n");

        const achados = violacoesEm("fake.jsx", quebrado);
        const palavras = [...new Set(achados.map((a) => a.palavra))].sort();

        expect(palavras).toEqual(["colecao", "mes", "nao", "selecao"]);
        expect(achados.every((a) => a.linha === 3 || a.linha === 4)).toBe(true);
    });

    it("ignora comentario, className, rota e import", () => {
        const aceitavel = [
            'import Botao from "@/components/ui/Botao.jsx";',
            "// colecao nao acentuada em comentario",
            "/* producao e pecas em bloco de comentario */",
            'const a = <div className="botao-nao-unico" />;',
            'const b = { to: "/colecoes/algodao", label: "Coleções" };',
            "const c = itens.map((titulo) => <li>{titulo}</li>);",
        ].join("\n");

        expect(violacoesEm("fake.jsx", aceitavel)).toEqual([]);
    });
});
