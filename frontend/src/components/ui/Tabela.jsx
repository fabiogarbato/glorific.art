import { FiChevronDown, FiChevronUp } from "react-icons/fi";
import Skeleton from "./Skeleton.jsx";

/**
 * Tabela do admin: densidade maior, linha com `border-sand` e zebra em `linen`.
 *
 * Contrato de coluna:
 *   { chave, titulo, render?(linha), ordenavel?, alinhamento?: 'esquerda'|'direita'|'centro', className? }
 *
 * Ordenacao e paginacao ficam com a page (`{ campo, direcao }`); aqui so o
 * cabecalho clicavel e o indicador.
 */
const ALINHAMENTO = {
    esquerda: "text-left",
    direita: "text-right",
    centro: "text-center",
};

function SortIcon({ ativo, direcao }) {
    if (!ativo) return <FiChevronDown size={13} className="opacity-25" aria-hidden="true" />;
    return direcao === "asc" ? (
        <FiChevronUp size={13} aria-hidden="true" />
    ) : (
        <FiChevronDown size={13} aria-hidden="true" />
    );
}

export default function Tabela({
    colunas = [],
    dados = [],
    chaveLinha = (linha, i) => linha?.id ?? i,
    ordenacao,
    onOrdenar,
    onLinhaClick,
    carregando = false,
    linhasSkeleton = 6,
    vazio = "Nenhum registro encontrado.",
    className = "",
    classeLinha,
}) {
    return (
        <div className={`w-full overflow-x-auto border border-sand bg-base-100 ${className}`}>
            <table className="w-full border-collapse text-sm">
                <thead>
                    <tr className="border-b border-sand bg-linen">
                        {colunas.map((col) => {
                            const ativo = ordenacao?.campo === col.chave;
                            const alinhamento = ALINHAMENTO[col.alinhamento] ?? ALINHAMENTO.esquerda;
                            return (
                                <th
                                    key={col.chave}
                                    scope="col"
                                    aria-sort={
                                        ativo
                                            ? ordenacao.direcao === "asc"
                                                ? "ascending"
                                                : "descending"
                                            : undefined
                                    }
                                    className={`px-4 py-3 font-sans text-xs font-medium uppercase tracking-widest text-ink-soft ${alinhamento} ${
                                        col.ordenavel && onOrdenar
                                            ? "cursor-pointer select-none hover:text-ink"
                                            : ""
                                    } ${col.className ?? ""}`}
                                    onClick={
                                        col.ordenavel && onOrdenar
                                            ? () => onOrdenar(col.chave)
                                            : undefined
                                    }
                                >
                                    <span className="inline-flex items-center gap-1">
                                        {col.titulo}
                                        {col.ordenavel && (
                                            <SortIcon ativo={ativo} direcao={ordenacao?.direcao} />
                                        )}
                                    </span>
                                </th>
                            );
                        })}
                    </tr>
                </thead>

                <tbody>
                    {carregando &&
                        Array.from({ length: linhasSkeleton }).map((_, i) => (
                            <tr key={`sk-${i}`} className="border-b border-sand/60">
                                {colunas.map((col) => (
                                    <td key={col.chave} className="px-4 py-3">
                                        <Skeleton className="h-4 w-full" />
                                    </td>
                                ))}
                            </tr>
                        ))}

                    {!carregando && dados.length === 0 && (
                        <tr>
                            <td
                                colSpan={colunas.length}
                                className="px-4 py-14 text-center text-sm text-ink-soft"
                            >
                                {vazio}
                            </td>
                        </tr>
                    )}

                    {!carregando &&
                        dados.map((linha, i) => (
                            <tr
                                key={chaveLinha(linha, i)}
                                onClick={onLinhaClick ? () => onLinhaClick(linha) : undefined}
                                className={`transition-colors even:bg-linen/50 ${
                                    onLinhaClick ? "cursor-pointer hover:bg-linen" : ""
                                } ${classeLinha ? classeLinha(linha) : "border-b border-sand/60"}`}
                            >
                                {colunas.map((col) => (
                                    <td
                                        key={col.chave}
                                        className={`px-4 py-3 align-middle text-ink ${
                                            ALINHAMENTO[col.alinhamento] ?? ALINHAMENTO.esquerda
                                        } ${col.className ?? ""}`}
                                    >
                                        {col.render ? col.render(linha) : linha?.[col.chave]}
                                    </td>
                                ))}
                            </tr>
                        ))}
                </tbody>
            </table>
        </div>
    );
}
