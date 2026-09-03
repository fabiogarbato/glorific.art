import { useContext } from "react";
import { AuthContext } from "@/contexts/AuthContext.jsx";

/** `const { usuario, login, logout, isAdmin } = useAuth();` */
export function useAuth() {
    const ctx = useContext(AuthContext);
    if (!ctx) {
        throw new Error("useAuth precisa estar dentro de <AuthProvider>.");
    }
    return ctx;
}

export default useAuth;
