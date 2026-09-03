import { Navigate, Outlet, useLocation } from "react-router-dom";
import { useAuth } from "@/hooks/useAuth.js";
import EsperandoSessao from "./EsperandoSessao.jsx";

/**
 * Guarda de sessao. Client-side e apenas de UX — a autorizacao de verdade e do
 * backend, que responde 401 de qualquer jeito.
 *
 * O guard le o CONTEXTO, e nao mais o token direto. O access token vive em
 * memoria: depois de um F5 ele so existe quando o refresh silencioso termina.
 * Decidir antes disso mandaria para o login todo mundo que atualiza a pagina em
 * /conta — por isso, enquanto `inicializando`, a tela espera.
 */
export default function RotaPrivada() {
    const location = useLocation();
    const { estaAutenticado, inicializando } = useAuth();

    if (inicializando) return <EsperandoSessao />;

    if (!estaAutenticado) {
        // `state.de` deixa o Login devolver a pessoa para onde ela tentou ir.
        return <Navigate to="/login" replace state={{ de: location }} />;
    }

    return <Outlet />;
}
