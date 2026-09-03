import { useMemo, useState } from "react";
import { Link, NavLink, Outlet } from "react-router-dom";
import {
    FiBox,
    FiChevronDown,
    FiDroplet,
    FiExternalLink,
    FiGrid,
    FiImage,
    FiLayers,
    FiLogOut,
    FiMenu,
    FiMessageSquare,
    FiPieChart,
    FiSettings,
    FiShoppingBag,
    FiSliders,
    FiStar,
    FiTag,
    FiTruck,
    FiUsers,
    FiX,
} from "react-icons/fi";

import { useAuth } from "@/hooks/useAuth.js";
import { usePermissoes } from "@/hooks/usePermissoes.js";
import { POLITICAS, rotularPapel } from "@/lib/permissoes.js";

/**
 * Chassi do painel: barra lateral fixa em `lg`, gaveta no mobile.
 *
 * O menu é filtrado pela POLICY de cada destino, com os mesmos nomes que o
 * backend usa. Isso não é autorização — quem autoriza é o servidor — mas evita
 * o pior desenho possível de painel com papéis: mostrar oito itens para o
 * operador de expedição e deixar sete deles devolverem 403 no clique.
 *
 * Grupo cujos itens sumiram por completo não aparece: um cabeçalho "Catálogo"
 * sozinho, sem nada embaixo, parece tela quebrada.
 */
const GRUPOS = [
    {
        chave: "grupoInicio",
        rotulo: null,
        itens: [
            {
                to: "/admin",
                label: "Início",
                Icone: FiPieChart,
                exato: true,
                policy: POLITICAS.PAINEL_ADMIN,
            },
        ],
    },
    {
        chave: "grupoCatalogo",
        rotulo: "Catálogo",
        itens: [
            {
                to: "/admin/produtos",
                label: "Produtos",
                Icone: FiTag,
                policy: POLITICAS.GESTAO_CATALOGO,
            },
            {
                to: "/admin/categorias",
                label: "Categorias",
                Icone: FiLayers,
                policy: POLITICAS.GESTAO_CATALOGO,
            },
            {
                to: "/admin/colecoes",
                label: "Coleções",
                Icone: FiStar,
                policy: POLITICAS.GESTAO_CATALOGO,
            },
            {
                to: "/admin/tamanhos",
                label: "Tamanhos",
                Icone: FiGrid,
                policy: POLITICAS.GESTAO_CATALOGO,
            },
            {
                to: "/admin/cores",
                label: "Cores",
                Icone: FiDroplet,
                policy: POLITICAS.GESTAO_CATALOGO,
            },
            {
                to: "/admin/midias",
                label: "Mídias",
                Icone: FiImage,
                policy: POLITICAS.GESTAO_CATALOGO,
            },
        ],
    },
    {
        chave: "grupoOperacao",
        rotulo: "Operação",
        itens: [
            {
                to: "/admin/pedidos",
                label: "Pedidos",
                Icone: FiShoppingBag,
                policy: POLITICAS.EXPEDICAO,
            },
            {
                to: "/admin/estoque",
                label: "Estoque",
                Icone: FiBox,
                policy: POLITICAS.EXPEDICAO,
            },
            {
                to: "/admin/cupons",
                label: "Cupons",
                Icone: FiTruck,
                policy: POLITICAS.GESTAO_CATALOGO,
            },
            {
                to: "/admin/avaliacoes",
                label: "Avaliações",
                Icone: FiMessageSquare,
                policy: POLITICAS.GESTAO_CATALOGO,
            },
        ],
    },
    {
        chave: "grupoConfiguracao",
        rotulo: "Configuração",
        itens: [
            {
                to: "/admin/configuracoes",
                label: "Configurações",
                Icone: FiSettings,
                policy: POLITICAS.SOMENTE_ADMIN,
            },
            {
                to: "/admin/usuarios",
                label: "Usuários",
                Icone: FiUsers,
                policy: POLITICAS.SOMENTE_ADMIN,
            },
        ],
    },
];

const item = ({ isActive }) =>
    `flex items-center gap-3 border-l-2 px-4 py-3 font-sans text-xs uppercase tracking-widest transition-colors ${
        isActive
            ? "border-brass bg-linen text-ink"
            : "border-transparent text-ink-soft hover:bg-linen/60 hover:text-ink"
    }`;

export default function LayoutAdmin() {
    const [gavetaAberta, setGavetaAberta] = useState(false);
    const [fechados, setFechados] = useState({});
    const { usuario, logout } = useAuth();
    const { pode, papeis } = usePermissoes();

    const grupos = useMemo(
        () =>
            GRUPOS.map((grupo) => ({
                ...grupo,
                itens: grupo.itens.filter((i) => pode(i.policy)),
            })).filter((grupo) => grupo.itens.length > 0),
        [pode],
    );

    const alternarGrupo = (chave) =>
        setFechados((atual) => ({ ...atual, [chave]: !atual[chave] }));

    const sidebar = (
        <div className="flex h-full flex-col">
            <div className="flex h-20 items-center justify-between border-b border-sand px-4">
                <Link to="/admin" className="font-display text-lg tracking-tight text-ink">
                    glorific<span className="text-brass">.art</span>
                </Link>
                <span className="eyebrow hidden lg:inline">Painel</span>
                <button
                    type="button"
                    aria-label="Fechar menu"
                    onClick={() => setGavetaAberta(false)}
                    className="flex h-11 w-11 items-center justify-center text-ink-soft lg:hidden"
                >
                    <FiX size={18} />
                </button>
            </div>

            <nav aria-label="Navegação do painel" className="flex-1 overflow-y-auto py-4">
                {grupos.map((grupo) => {
                    const fechado = !!fechados[grupo.chave];

                    return (
                        <div key={grupo.chave} className="mb-2">
                            {grupo.rotulo && (
                                <button
                                    type="button"
                                    aria-expanded={!fechado}
                                    onClick={() => alternarGrupo(grupo.chave)}
                                    className="flex w-full items-center justify-between px-4 pb-1 pt-4 text-xs uppercase tracking-widest text-taupe transition-colors hover:text-ink-soft"
                                >
                                    {grupo.rotulo}
                                    <FiChevronDown
                                        size={13}
                                        aria-hidden="true"
                                        className={`transition-transform ${fechado ? "-rotate-90" : ""}`}
                                    />
                                </button>
                            )}

                            {!fechado &&
                                grupo.itens.map(({ to, label, Icone, exato }) => (
                                    <NavLink
                                        key={to}
                                        to={to}
                                        end={exato}
                                        className={item}
                                        onClick={() => setGavetaAberta(false)}
                                    >
                                        <Icone size={16} aria-hidden="true" />
                                        {label}
                                    </NavLink>
                                ))}
                        </div>
                    );
                })}
            </nav>

            <div className="border-t border-sand p-4">
                <p className="truncate text-xs text-ink-soft">{usuario?.email}</p>
                {papeis.length > 0 && (
                    <p className="mt-1 flex items-center gap-1 text-xs text-taupe">
                        <FiSliders size={11} aria-hidden="true" />
                        {papeis.map(rotularPapel).join(" · ")}
                    </p>
                )}

                <div className="mt-3 flex flex-col gap-2">
                    <Link
                        to="/"
                        className="inline-flex items-center gap-2 font-sans text-xs uppercase tracking-widest text-ink-soft hover:text-ink"
                    >
                        <FiExternalLink size={14} /> Ver a loja
                    </Link>
                    <button
                        type="button"
                        onClick={() => logout(true)}
                        className="inline-flex items-center gap-2 font-sans text-xs uppercase tracking-widest text-ink-soft hover:text-danger"
                    >
                        <FiLogOut size={14} /> Sair
                    </button>
                </div>
            </div>
        </div>
    );

    return (
        <div className="flex min-h-screen bg-base-100 text-base-content">
            <aside className="hidden w-64 shrink-0 border-r border-sand bg-base-100 lg:block">
                <div className="sticky top-0 h-screen">{sidebar}</div>
            </aside>

            {gavetaAberta && (
                <div className="fixed inset-0 z-overlay lg:hidden">
                    <button
                        type="button"
                        aria-label="Fechar menu"
                        className="absolute inset-0 h-full w-full bg-ink/40"
                        onClick={() => setGavetaAberta(false)}
                    />
                    <div className="relative h-full w-72 border-r border-sand bg-base-100">
                        {sidebar}
                    </div>
                </div>
            )}

            <div className="flex min-w-0 flex-1 flex-col">
                <div className="flex h-16 items-center gap-3 border-b border-sand px-4 lg:hidden">
                    <button
                        type="button"
                        aria-label="Abrir menu"
                        onClick={() => setGavetaAberta(true)}
                        className="flex h-11 w-11 items-center justify-center text-ink"
                    >
                        <FiMenu size={20} />
                    </button>
                    <span className="font-display text-lg tracking-tight text-ink">
                        Painel administrativo
                    </span>
                </div>

                <main className="flex-1 px-4 py-6 sm:px-6 lg:px-8">
                    <Outlet />
                </main>
            </div>
        </div>
    );
}
