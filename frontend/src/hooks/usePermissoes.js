import { useMemo } from "react";
import { useAuth } from "@/hooks/useAuth.js";
import {
    ehAdministrativo,
    papeisDoToken,
    POLITICAS,
    satisfaz,
} from "@/lib/permissoes.js";

/**
 * Papeis da sessao + teste de policy.
 *
 *   const { pode, papeis } = usePermissoes();
 *   if (pode(POLITICAS.SOMENTE_ADMIN)) { ... }
 *
 * Le a claim direto do JWT porque um usuario pode ter MAIS DE UM papel e o
 * `AuthContext` guarda so o primeiro (`usuario.role`). Depende de `usuario`
 * apenas para recalcular quando a sessao troca — o valor sai sempre do token.
 *
 * Serve para ESCONDER o que nao adianta clicar. A autorizacao real e a policy
 * do servidor, sempre.
 */
export function usePermissoes() {
    const { usuario } = useAuth();

    return useMemo(() => {
        const papeis = papeisDoToken();

        return {
            papeis,
            administrativo: ehAdministrativo(papeis),
            pode: (politica) => satisfaz(politica, papeis),
            temPapel: (papel) => papeis.includes(papel),
            ehAdmin: satisfaz(POLITICAS.SOMENTE_ADMIN, papeis),
        };
        // `usuario?.exp` muda a cada login/logout — e o gatilho certo para reler.
        // eslint-disable-next-line react-hooks/exhaustive-deps
    }, [usuario?.id, usuario?.exp]);
}

export default usePermissoes;
