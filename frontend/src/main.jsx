import { StrictMode } from "react";
import { createRoot } from "react-dom/client";
import { BrowserRouter } from "react-router-dom";
import { QueryClient, QueryClientProvider } from "@tanstack/react-query";
import { ReactQueryDevtools } from "@tanstack/react-query-devtools";

import App from "./App.jsx";
import ProvedorGoogle from "@/components/auth/ProvedorGoogle.jsx";
import { AuthProvider } from "@/contexts/AuthContext.jsx";
import { ToastProvider } from "@/contexts/ToastContext.jsx";
import { CarrinhoProvider } from "@/contexts/CarrinhoContext.jsx";
import "./styles/index.css";

/**
 * `retry: 1` porque a maioria das falhas aqui e 4xx (retentar nao ajuda);
 * `refetchOnWindowFocus: false` para o admin nao recarregar tabela a cada
 * alt-tab.
 */
const queryClient = new QueryClient({
    defaultOptions: {
        queries: {
            retry: 1,
            refetchOnWindowFocus: false,
            staleTime: 1000 * 30,
        },
    },
});

// A ordem importa. ProvedorGoogle por fora de tudo: o script do Google Identity
// precisa inicializar UMA vez por carga da pagina — dentro do BotaoGoogle ele
// remontava a cada tecla digitada no formulario, e era isso que fazia o widget
// piscar ou nao aparecer. Depois: AuthProvider fora do Router (nao depende de
// rota); ToastProvider dentro, porque e ele que assina o toastBus do axios;
// CarrinhoProvider por ultimo, ja com toast disponivel.
createRoot(document.getElementById("root")).render(
    <StrictMode>
        <ProvedorGoogle>
            <QueryClientProvider client={queryClient}>
                <AuthProvider>
                    <BrowserRouter>
                        <ToastProvider>
                            <CarrinhoProvider>
                                <App />
                            </CarrinhoProvider>
                        </ToastProvider>
                    </BrowserRouter>
                </AuthProvider>
                {import.meta.env.DEV && <ReactQueryDevtools initialIsOpen={false} />}
            </QueryClientProvider>
        </ProvedorGoogle>
    </StrictMode>,
);
