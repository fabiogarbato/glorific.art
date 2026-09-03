import { Navigate, Outlet, useLocation } from "react-router-dom";
import { getToken, tokenValido } from "@/api/client.js";
import { tokenSatisfaz } from "@/lib/permissoes.js";

/**
 * Guarda por POLICY, com os mesmos nomes do backend
 * ("SomenteAdmin", "GestaoCatalogo", "Expedicao", "PainelAdmin").
 *
 * Existe porque `RotaAdmin` compara `usuario.role === "admin"` e o sistema tem
 * quatro papéis: com aquela guarda, gerente e operador não entram no painel de
 * jeito nenhum, mesmo tendo policy no servidor. Aqui a decisão sai da mesma
 * tabela que o backend usa, e a claim de papel é lida como lista.
 *
 * Continua sendo apenas UX: quem autoriza de verdade é a policy do servidor.
 *
 * Uso:
 *   <Route element={<RotaPolicy policy="Expedicao" />}>
 *       <Route path="/admin/pedidos" element={<ListaPedidos />} />
 *   </Route>
 *
 * Também aceita `children` para envolver um elemento só:
 *   <RotaPolicy policy="SomenteAdmin"><Usuarios /></RotaPolicy>
 */
export default function RotaPolicy({ policy, children, redirecionarPara = "/admin" }) {
    const location = useLocation();
    const token = getToken();

    if (!token || !tokenValido(token)) {
        return <Navigate to="/login" replace state={{ de: location }} />;
    }

    if (!tokenSatisfaz(policy, token)) {
        // Quem está logado mas não tem o papel volta para a porta do painel — e
        // não para o login, que daria a impressão errada de sessão expirada.
        return <Navigate to={redirecionarPara} replace />;
    }

    return children ?? <Outlet />;
}
