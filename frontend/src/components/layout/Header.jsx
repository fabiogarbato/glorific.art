import { useState } from "react";
import { Link, NavLink, useNavigate } from "react-router-dom";
import { FiMenu, FiSearch, FiShoppingBag, FiX } from "react-icons/fi";
import MenuConta from "./MenuConta.jsx";
import { STORE } from "@/data/store.js";
import { useCarrinho } from "@/hooks/useCarrinho.js";

const linkClasses = ({ isActive }) =>
    `font-sans text-xs uppercase tracking-widest transition-colors ${
        isActive ? "text-ink" : "text-ink-soft hover:text-ink"
    }`;

export default function Header() {
    const [menuAberto, setMenuAberto] = useState(false);
    const [buscaAberta, setBuscaAberta] = useState(false);
    const [termo, setTermo] = useState("");
    const navigate = useNavigate();

    const { totalItens, abrir } = useCarrinho();

    function submeterBusca(e) {
        e.preventDefault();
        const q = termo.trim();
        if (!q) return;
        setBuscaAberta(false);
        setMenuAberto(false);
        navigate(`/busca?q=${encodeURIComponent(q)}`);
    }

    return (
        <header className="sticky top-0 z-header border-b border-sand bg-base-100/95 backdrop-blur">
            {/* Faixa de aviso — o unico bloco escuro do topo. */}
            <div className="bg-ink px-4 py-2 text-center">
                <p className="font-sans text-[11px] uppercase tracking-widest text-bone">
                    Frete cortesia acima de R$ 399 · Parcelamento em até 6x
                </p>
            </div>

            <div className="shell flex h-20 items-center justify-between gap-6">
                <button
                    type="button"
                    aria-label={menuAberto ? "Fechar menu" : "Abrir menu"}
                    aria-expanded={menuAberto}
                    onClick={() => setMenuAberto((v) => !v)}
                    className="flex h-11 w-11 items-center justify-center text-ink lg:hidden"
                >
                    {menuAberto ? <FiX size={20} /> : <FiMenu size={20} />}
                </button>

                <Link to="/" className="shrink-0" aria-label={`${STORE.name} — início`}>
                    <span className="font-display text-xl tracking-tight text-ink sm:text-2xl">
                        glorific
                    </span>
                    <span className="font-display text-xl tracking-tight text-brass sm:text-2xl">
                        .art
                    </span>
                </Link>

                <nav aria-label="Principal" className="hidden items-center gap-8 lg:flex">
                    {STORE.navegacao.map((item) => (
                        <NavLink key={item.to} to={item.to} className={linkClasses}>
                            {item.label}
                        </NavLink>
                    ))}
                </nav>

                <div className="flex items-center gap-1">
                    <form
                        onSubmit={submeterBusca}
                        role="search"
                        className={`items-center border-b border-sand transition-all ${
                            buscaAberta ? "flex w-40 sm:w-56" : "hidden xl:flex xl:w-52"
                        }`}
                    >
                        <label htmlFor="busca-topo" className="sr-only">
                            Buscar produtos
                        </label>
                        <input
                            id="busca-topo"
                            type="search"
                            value={termo}
                            onChange={(e) => setTermo(e.target.value)}
                            placeholder="Buscar"
                            className="h-9 w-full bg-transparent font-sans text-sm text-ink placeholder:text-taupe focus:outline-none"
                        />
                        <button
                            type="submit"
                            aria-label="Buscar"
                            className="flex h-9 w-9 items-center justify-center text-ink-soft hover:text-ink"
                        >
                            <FiSearch size={17} />
                        </button>
                    </form>

                    <button
                        type="button"
                        aria-label="Abrir busca"
                        onClick={() => setBuscaAberta((v) => !v)}
                        className="flex h-11 w-11 items-center justify-center text-ink-soft hover:text-ink xl:hidden"
                    >
                        <FiSearch size={18} />
                    </button>

                    {/*
                     * O ícone de pessoa abre menu, e não um link direto: é ele
                     * que revela "Painel administrativo" para admin, gerente e
                     * operador. Sem isso o painel não tinha entrada nenhuma na
                     * loja — só a URL digitada à mão.
                     */}
                    <MenuConta />

                    <button
                        type="button"
                        onClick={abrir}
                        aria-label={`Carrinho com ${totalItens} ${totalItens === 1 ? "item" : "itens"}`}
                        className="relative flex h-11 w-11 items-center justify-center text-ink hover:text-olive"
                    >
                        <FiShoppingBag size={19} />
                        {totalItens > 0 && (
                            <span className="absolute right-1 top-1 flex h-4 min-w-4 items-center justify-center bg-olive px-1 font-sans text-[10px] leading-none text-bone">
                                {totalItens}
                            </span>
                        )}
                    </button>
                </div>
            </div>

            {menuAberto && (
                <nav aria-label="Menu mobile" className="border-t border-sand bg-base-100 lg:hidden">
                    <ul className="shell flex flex-col py-2">
                        {STORE.navegacao.map((item) => (
                            <li key={item.to}>
                                <NavLink
                                    to={item.to}
                                    onClick={() => setMenuAberto(false)}
                                    className="block border-b border-sand/60 py-4 font-sans text-xs uppercase tracking-widest text-ink"
                                >
                                    {item.label}
                                </NavLink>
                            </li>
                        ))}
                    </ul>
                </nav>
            )}
        </header>
    );
}
