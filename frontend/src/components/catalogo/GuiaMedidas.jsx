import Modal from "@/components/ui/Modal.jsx";

/**
 * Guia de medidas em modal — item numero 1 de reducao de devolucao em moda.
 *
 * A tabela vem do proprio produto (`produto.tabelaMedidas`). So entram as
 * colunas que a peca realmente tem: uma saia nao tem busto nem manga, e coluna
 * vazia so faz a pessoa duvidar do resto da tabela.
 */
const COLUNAS = [
    { chave: "bustoCm", rotulo: "Busto" },
    { chave: "cinturaCm", rotulo: "Cintura" },
    { chave: "quadrilCm", rotulo: "Quadril" },
    { chave: "comprimentoCm", rotulo: "Comprimento" },
    { chave: "mangaCm", rotulo: "Manga" },
];

function formatarCm(valor) {
    if (valor === null || valor === undefined) return "—";
    const numero = Number(valor);
    if (!Number.isFinite(numero)) return "—";
    const medida = numero.toLocaleString("pt-BR", { maximumFractionDigits: 1 });
    return `${medida} cm`;
}

export default function GuiaMedidas({ tabela, isOpen, onClose }) {
    const linhas = [...(tabela?.linhas ?? [])].sort((a, b) => a.ordem - b.ordem);

    const colunas = COLUNAS.filter((coluna) =>
        linhas.some((linha) => linha[coluna.chave] !== null && linha[coluna.chave] !== undefined),
    );

    return (
        <Modal
            isOpen={isOpen}
            onClose={onClose}
            titulo={tabela?.nome || "Guia de medidas"}
            largura="lg"
        >
            {linhas.length === 0 ? (
                <p className="text-sm">
                    Esta peça ainda não tem tabela de medidas publicada. Fale com a gente que
                    conferimos a peça na régua para você.
                </p>
            ) : (
                <>
                    <p className="text-sm">
                        Medidas do CORPO, em centímetros. Na dúvida entre dois tamanhos, veja o
                        caimento nas avaliações de quem já comprou.
                    </p>

                    <div className="mt-5 overflow-x-auto">
                        <table className="w-full min-w-[30rem] border-collapse text-sm">
                            <caption className="sr-only">
                                Medidas do corpo por tamanho, em centímetros
                            </caption>
                            <thead>
                                <tr className="border-b border-sand text-left">
                                    <th scope="col" className="py-2 pr-4 eyebrow">
                                        Tamanho
                                    </th>
                                    {colunas.map((coluna) => (
                                        <th
                                            key={coluna.chave}
                                            scope="col"
                                            className="py-2 pr-4 eyebrow"
                                        >
                                            {coluna.rotulo}
                                        </th>
                                    ))}
                                </tr>
                            </thead>
                            <tbody>
                                {linhas.map((linha, i) => (
                                    <tr
                                        key={linha.id ?? linha.idTamanho}
                                        className={`border-b border-sand ${
                                            i % 2 === 1 ? "bg-linen" : ""
                                        }`}
                                    >
                                        <th
                                            scope="row"
                                            className="py-2.5 pr-4 text-left font-sans text-xs uppercase tracking-widest text-ink"
                                        >
                                            {linha.codigoTamanho}
                                        </th>
                                        {colunas.map((coluna) => (
                                            <td
                                                key={coluna.chave}
                                                className="py-2.5 pr-4 tabular-nums text-ink-soft"
                                            >
                                                {formatarCm(linha[coluna.chave])}
                                            </td>
                                        ))}
                                    </tr>
                                ))}
                            </tbody>
                        </table>
                    </div>

                    {tabela?.observacao && (
                        <p className="mt-5 border-t border-sand pt-4 text-sm">
                            {tabela.observacao}
                        </p>
                    )}
                </>
            )}
        </Modal>
    );
}
