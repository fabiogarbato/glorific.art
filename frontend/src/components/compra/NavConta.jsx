import { NavLink } from "react-router-dom";

/**
 * Navegação da área logada. Fica no topo em telas pequenas e vira coluna à
 * esquerda a partir de `lg` — mesma gramática editorial do resto da loja: sem
 * caixa, só o filete e o peso do texto marcando onde a pessoa está.
 */
const ITENS = [
    { to: "/conta", fim: true, rotulo: "Perfil" },
    { to: "/conta/pedidos", rotulo: "Meus pedidos" },
    { to: "/conta/enderecos", rotulo: "Endereços" },
    { to: "/conta/lista-desejos", rotulo: "Lista de desejos" },
];

export default function NavConta() {
    return (
        <nav aria-label="Minha conta" className="lg:sticky lg:top-32">
            <ul className="flex gap-x-6 gap-y-2 overflow-x-auto border-b border-sand pb-3 lg:flex-col lg:overflow-visible lg:border-b-0 lg:pb-0">
                {ITENS.map((item) => (
                    <li key={item.to} className="shrink-0">
                        <NavLink
                            to={item.to}
                            end={item.fim}
                            className={({ isActive }) =>
                                `block whitespace-nowrap py-1 font-sans text-xs uppercase tracking-widest transition-colors lg:border-l lg:py-2 lg:pl-4 ${
                                    isActive
                                        ? "text-ink lg:border-olive"
                                        : "text-ink-soft hover:text-ink lg:border-sand"
                                }`
                            }
                        >
                            {item.rotulo}
                        </NavLink>
                    </li>
                ))}
            </ul>
        </nav>
    );
}

/** Chassi comum das telas de conta: título, navegação lateral e conteúdo. */
export function LayoutConta({ titulo, descricao, acoes, children }) {
    return (
        <div className="shell py-12 lg:py-16">
            <header className="flex flex-wrap items-end justify-between gap-4 pb-8">
                <div>
                    <p className="eyebrow">Minha conta</p>
                    <h1 className="mt-3 font-display text-2xl tracking-tight text-ink sm:text-3xl">
                        {titulo}
                    </h1>
                    {descricao && (
                        <p className="mt-3 max-w-xl text-base leading-relaxed text-ink-soft">
                            {descricao}
                        </p>
                    )}
                </div>
                {acoes}
            </header>

            <div className="grid gap-10 lg:grid-cols-[220px_1fr] lg:gap-16">
                <NavConta />
                <div className="min-w-0">{children}</div>
            </div>
        </div>
    );
}
