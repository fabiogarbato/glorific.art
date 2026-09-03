import { Navigate, Outlet, useLocation } from "react-router-dom";
import { useAuth } from "@/hooks/useAuth.js";
import { PAPEIS_ADMINISTRATIVOS } from "@/lib/constants.js";
import EsperandoSessao from "./EsperandoSessao.jsx";

/**
 * Guarda do painel.
 *
 * Papel administrativo nao e so "admin": `Roles.Administrativos` no backend traz
 * admin, gerente e operador, e o guard precisa aceitar os tres — checar so
 * "admin" trancaria gerente e operador fora do painel que eles administram.
 *
 * Os dois desvios sao diferentes de proposito:
 * - sem sessao  -> /login, guardando de onde veio;
 * - sem papel   -> home. Mandar para o login quem ja esta logado sugeriria
 *   "sua sessao expirou", quando o caso e "esta area nao e sua".
 *
 * Isto e UX. Quem autoriza de verdade e a policy do servidor.
 */
export default function RotaAdmin() {
    const location = useLocation();
    const { usuario, estaAutenticado, inicializando } = useAuth();

    if (inicializando) return <EsperandoSessao />;

    if (!estaAutenticado) {
        return <Navigate to="/login" replace state={{ de: location }} />;
    }

    const permitido = (usuario?.papeis ?? []).some((papel) =>
        PAPEIS_ADMINISTRATIVOS.includes(papel),
    );

    if (!permitido) return <Navigate to="/" replace />;

    return <Outlet />;
}
