import { useContext } from "react";
import { CarrinhoContext } from "@/contexts/CarrinhoContext.jsx";

export function useCarrinho() {
    const ctx = useContext(CarrinhoContext);
    if (!ctx) {
        throw new Error("useCarrinho precisa estar dentro de <CarrinhoProvider>.");
    }
    return ctx;
}

export default useCarrinho;
