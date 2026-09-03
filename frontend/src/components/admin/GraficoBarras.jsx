/**
 * Gráfico de barras horizontais em CSS puro — sem nenhuma biblioteca nova.
 *
 * Barra horizontal e não vertical de propósito: os rótulos aqui são frases
 * ("Aguardando pagamento", "Pagamento recusado") e barra vertical obrigaria a
 * girar o texto. Horizontal também sobrevive ao celular sem virar carrossel.
 *
 * A estrutura é uma lista de definição: quem usa leitor de tela ouve
 * "Pago, 12 pedidos" em vez de "gráfico, imagem".
 *
 * `dados`: [{ chave, rotulo, valor, apoio?, tom? }]
 */
const TOM = {
    neutro: "bg-olive",
    alerta: "bg-warning",
    critico: "bg-danger",
    acento: "bg-brass",
};

export default function GraficoBarras({ dados = [], formatarValor = (v) => v, className = "" }) {
    const maximo = dados.reduce((maior, d) => Math.max(maior, Number(d.valor) || 0), 0);

    return (
        <dl className={`flex flex-col gap-3 ${className}`}>
            {dados.map((item) => {
                const valor = Number(item.valor) || 0;
                // Barra de valor zero ainda aparece como filete: some por completo
                // daria a impressão de que a linha não existe no relatório.
                const largura = maximo > 0 ? Math.max((valor / maximo) * 100, 1.5) : 1.5;

                return (
                    <div key={item.chave} className="grid grid-cols-[minmax(0,10rem)_1fr] items-center gap-3 sm:grid-cols-[minmax(0,14rem)_1fr]">
                        <dt className="truncate text-xs uppercase tracking-widest text-ink-soft">
                            {item.rotulo}
                        </dt>
                        <dd className="flex items-center gap-3">
                            <div className="h-3 min-w-0 flex-1 bg-sand/60">
                                <div
                                    className={`h-full ${TOM[item.tom] ?? TOM.neutro}`}
                                    style={{ width: `${largura}%` }}
                                />
                            </div>
                            <span className="preco w-24 shrink-0 text-right text-sm text-ink">
                                {formatarValor(valor)}
                            </span>
                            {item.apoio && (
                                <span className="preco hidden w-28 shrink-0 text-right text-xs text-ink-soft sm:inline">
                                    {item.apoio}
                                </span>
                            )}
                        </dd>
                    </div>
                );
            })}
        </dl>
    );
}
