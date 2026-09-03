import { useEffect, useId, useRef, useState } from "react";
import { Link } from "react-router-dom";
import { FiUser } from "react-icons/fi";
import { useAuth } from "@/hooks/useAuth.js";

/**
 * Menu da conta, no ícone de pessoa do cabeçalho.
 *
 * Antes daqui o ícone era um link solto para /login ou /conta, e o painel
 * administrativo não tinha porta de entrada nenhuma na loja: quem administra
 * precisava digitar /admin na barra de endereço, e quem estava sem sessão caía
 * numa tela de login sem uma linha explicando por quê. Este menu é essa porta.
 *
 * O item do painel aparece para PAPEL ADMINISTRATIVO, que são três papéis
 * (admin, gerente e operador) — a mesma lista de `Roles.Administrativos` no
 * backend, já resolvida em `usuario.isAdmin` pelo AuthContext. Esconder o item
 * é conveniência, não segurança: quem autoriza é a policy do servidor, e a
 * RotaAdmin ainda barra o acesso direto pela URL.
 *
 * Acessibilidade (o motivo de isto ser um componente, e não três linhas soltas
 * no Header): abre por clique ou seta para baixo, fecha com Esc, com clique fora
 * e ao escolher um item; o foco entra no primeiro item ao abrir e volta para o
 * botão ao fechar; as setas percorrem a lista; `aria-expanded` acompanha o
 * estado de verdade. É um menu de navegação, e não um diálogo — por isso não
 * usa `useDialog`: trava de rolagem e `aria-modal` num dropdown de cabeçalho
 * prendem a página inteira por causa de um menu de quatro itens.
 */

const ITENS_VISITANTE = [
    { to: "/login", rotulo: "Entrar" },
    { to: "/cadastro", rotulo: "Criar cadastro" },
];

const ITENS_CLIENTE = [
    { to: "/conta", rotulo: "Minha conta" },
    { to: "/conta/pedidos", rotulo: "Meus pedidos" },
    { to: "/conta/lista-desejos", rotulo: "Lista de desejos" },
];

const ITEM =
    "block w-full px-4 py-3 text-left font-sans text-xs uppercase tracking-widest " +
    "text-ink-soft transition-colors hover:bg-linen hover:text-ink " +
    "focus:bg-linen focus:text-ink focus:outline-none";

/** Só o primeiro nome cabe no cabeçalho sem quebrar a linha. */
function primeiroNome(usuario) {
    const nome = (usuario?.nome ?? "").trim();
    if (nome) return nome.split(/\s+/)[0];
    return (usuario?.email ?? "").split("@")[0] || "sua conta";
}

export default function MenuConta() {
    const [aberto, setAberto] = useState(false);
    const { usuario, isAdmin, logout } = useAuth();

    const caixaRef = useRef(null);
    const botaoRef = useRef(null);
    const menuRef = useRef(null);
    const idMenu = useId();

    /** Itens focáveis, na ordem em que estão na tela. */
    function focaveis() {
        return Array.from(menuRef.current?.querySelectorAll('[role="menuitem"]') ?? []);
    }

    function fechar({ devolverFoco = true } = {}) {
        setAberto(false);
        if (devolverFoco) botaoRef.current?.focus({ preventScroll: true });
    }

    // Clique fora fecha. `mousedown` e não `click`: o menu precisa sumir no
    // instante em que a pessoa aperta o botão do mouse noutro lugar da página,
    // e não depois que o clique já ativou o que estava embaixo.
    useEffect(() => {
        if (!aberto) return undefined;

        const aoApontar = (e) => {
            if (!caixaRef.current?.contains(e.target)) setAberto(false);
        };

        document.addEventListener("mousedown", aoApontar);
        return () => document.removeEventListener("mousedown", aoApontar);
    }, [aberto]);

    // Ao abrir, o foco entra no primeiro item — é o que faz o menu existir para
    // quem navega por teclado.
    useEffect(() => {
        if (aberto) focaveis()[0]?.focus({ preventScroll: true });
    }, [aberto]);

    function aoTeclarNoBotao(e) {
        if (e.key === "ArrowDown" || e.key === "ArrowUp") {
            e.preventDefault();
            setAberto(true);
        } else if (e.key === "Escape" && aberto) {
            e.preventDefault();
            fechar();
        }
    }

    function aoTeclarNoMenu(e) {
        const itens = focaveis();
        const atual = itens.indexOf(document.activeElement);

        if (e.key === "Escape") {
            e.preventDefault();
            fechar();
            return;
        }

        // Tab sai do menu: fecha sem roubar o foco, para a navegação seguir para
        // o próximo controle do cabeçalho.
        if (e.key === "Tab") {
            setAberto(false);
            return;
        }

        if (e.key === "ArrowDown") {
            e.preventDefault();
            itens[(atual + 1) % itens.length]?.focus();
        } else if (e.key === "ArrowUp") {
            e.preventDefault();
            itens[(atual - 1 + itens.length) % itens.length]?.focus();
        } else if (e.key === "Home") {
            e.preventDefault();
            itens[0]?.focus();
        } else if (e.key === "End") {
            e.preventDefault();
            itens[itens.length - 1]?.focus();
        }
    }

    async function sair() {
        setAberto(false);
        // `redirecionar` recarrega a loja na home. É recarga de verdade de
        // propósito: leva junto o cache do React Query, que pode ter tabela do
        // painel e dados de pedido da sessão que acabou de terminar.
        await logout({ redirecionar: true });
    }

    const itens = usuario ? ITENS_CLIENTE : ITENS_VISITANTE;

    return (
        <div className="relative" ref={caixaRef}>
            <button
                type="button"
                ref={botaoRef}
                onClick={() => setAberto((v) => !v)}
                onKeyDown={aoTeclarNoBotao}
                aria-haspopup="menu"
                aria-expanded={aberto}
                aria-controls={aberto ? idMenu : undefined}
                aria-label={usuario ? "Minha conta" : "Entrar ou criar cadastro"}
                className="flex h-11 w-11 items-center justify-center text-ink-soft transition-colors hover:text-ink focus-visible:outline-none focus-visible:ring-2 focus-visible:ring-olive focus-visible:ring-offset-2 focus-visible:ring-offset-base-100"
            >
                <FiUser size={18} />
            </button>

            {aberto && (
                <div
                    id={idMenu}
                    ref={menuRef}
                    role="menu"
                    aria-label="Conta"
                    onKeyDown={aoTeclarNoMenu}
                    className="absolute right-0 top-full z-dropdown mt-2 w-60 border border-sand bg-base-100 py-2 shadow-[0_12px_32px_-16px_rgba(28,26,23,0.35)]"
                >
                    {usuario && (
                        <div className="border-b border-sand px-4 pb-3 pt-1">
                            <p className="font-display text-lg leading-tight text-ink">
                                Olá, {primeiroNome(usuario)}
                            </p>
                            {usuario.email && (
                                <p className="mt-1 truncate text-xs text-taupe">
                                    {usuario.email}
                                </p>
                            )}
                        </div>
                    )}

                    {isAdmin && (
                        <Link
                            to="/admin"
                            role="menuitem"
                            onClick={() => setAberto(false)}
                            className={`${ITEM} border-b border-sand bg-linen/60 font-medium text-olive hover:bg-linen hover:text-olive-dp focus:text-olive-dp`}
                        >
                            Painel administrativo
                        </Link>
                    )}

                    {itens.map((item) => (
                        <Link
                            key={item.to}
                            to={item.to}
                            role="menuitem"
                            onClick={() => setAberto(false)}
                            className={ITEM}
                        >
                            {item.rotulo}
                        </Link>
                    ))}

                    {usuario && (
                        <button
                            type="button"
                            role="menuitem"
                            onClick={sair}
                            className={`${ITEM} mt-1 border-t border-sand`}
                        >
                            Sair
                        </button>
                    )}
                </div>
            )}
        </div>
    );
}
