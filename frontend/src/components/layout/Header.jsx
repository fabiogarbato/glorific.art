import { useEffect, useState } from "react";
import { Link, NavLink, useLocation, useNavigate } from "react-router-dom";
import { FiMenu, FiSearch, FiShoppingBag, FiX } from "react-icons/fi";
import MenuConta from "./MenuConta.jsx";
import { STORE } from "@/data/store.js";
import { useCarrinho } from "@/hooks/useCarrinho.js";

// Limiar de rolagem para o logo grande do topo da Home encolher.
const LIMIAR_ROLAGEM = 48;

const linkClasses = ({ isActive }) =>
    `font-sans text-xs uppercase tracking-widest transition-colors ${
        isActive ? "text-bone" : "text-bone/70 hover:text-bone"
    }`;

export default function Header() {
    const [menuAberto, setMenuAberto] = useState(false);
    const [buscaAberta, setBuscaAberta] = useState(false);
    const [termo, setTermo] = useState("");
    const [rolou, setRolou] = useState(false);
    const navigate = useNavigate();
    const { pathname } = useLocation();

    const { totalItens, abrir } = useCarrinho();

    // O logo grande, saindo da barra, e um efeito so do topo da Home — nas
    // outras paginas (e na propria Home ja rolada) ele fica no tamanho normal.
    const naHome = pathname === "/";

    // A rolagem em si (barra solida no topo, vitrificada depois) vale pra
    // qualquer pagina, nao so a Home.
    useEffect(() => {
        function aoRolar() {
            setRolou(window.scrollY > LIMIAR_ROLAGEM);
        }
        aoRolar();
        window.addEventListener("scroll", aoRolar, { passive: true });
        return () => window.removeEventListener("scroll", aoRolar);
    }, []);

    const logoGrande = naHome && !rolou;

    function submeterBusca(e) {
        e.preventDefault();
        const q = termo.trim();
        if (!q) return;
        setBuscaAberta(false);
        setMenuAberto(false);
        navigate(`/busca?q=${encodeURIComponent(q)}`);
    }

    return (
        <header
            className={`sticky top-0 z-header border-b border-transparent transition-colors duration-300 ${
                rolou ? "bg-ink/75 backdrop-blur-md" : "bg-ink"
            }`}
        >
            <div className="shell flex h-24 items-center justify-between gap-6">
                <button
                    type="button"
                    aria-label={menuAberto ? "Fechar menu" : "Abrir menu"}
                    aria-expanded={menuAberto}
                    onClick={() => setMenuAberto((v) => !v)}
                    className="flex h-11 w-11 items-center justify-center text-bone lg:hidden"
                >
                    {menuAberto ? <FiX size={20} /> : <FiMenu size={20} />}
                </button>

                <Link to="/" className="shrink-0" aria-label={`${STORE.name}, início`}>
                    <img
                        src="/hero-logo-mark.png"
                        alt={STORE.name}
                        className={`w-auto transition-all duration-300 ${
                            logoGrande
                                ? "h-28 translate-y-6 sm:h-36 sm:translate-y-8"
                                : "h-16 sm:h-20"
                        }`}
                    />
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
                        className={`items-center border-b border-bone/30 transition-all ${
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
                            className="h-9 w-full bg-transparent font-sans text-sm text-bone placeholder:text-bone/50 focus:outline-none"
                        />
                        <button
                            type="submit"
                            aria-label="Buscar"
                            className="flex h-9 w-9 items-center justify-center text-bone/80 hover:text-bone"
                        >
                            <FiSearch size={17} />
                        </button>
                    </form>

                    <button
                        type="button"
                        aria-label="Abrir busca"
                        onClick={() => setBuscaAberta((v) => !v)}
                        className="flex h-11 w-11 items-center justify-center text-bone/80 hover:text-bone xl:hidden"
                    >
                        <FiSearch size={18} />
                    </button>

                    {/*
                     * O ícone de pessoa abre menu, e não um link direto: é ele
                     * que revela "Painel administrativo" para admin, gerente e
                     * operador. Sem isso o painel não tinha entrada nenhuma na
                     * loja — só a URL digitada à mão.
                     */}
                    <MenuConta transparente />

                    <button
                        type="button"
                        onClick={abrir}
                        aria-label={`Carrinho com ${totalItens} ${totalItens === 1 ? "item" : "itens"}`}
                        className="relative flex h-11 w-11 items-center justify-center text-bone hover:text-brass"
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
                <nav aria-label="Menu mobile" className="border-t border-bone/10 bg-ink lg:hidden">
                    <ul className="shell flex flex-col py-2">
                        {STORE.navegacao.map((item) => (
                            <li key={item.to}>
                                <NavLink
                                    to={item.to}
                                    onClick={() => setMenuAberto(false)}
                                    className="block border-b border-bone/10 py-4 font-sans text-xs uppercase tracking-widest text-bone"
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
