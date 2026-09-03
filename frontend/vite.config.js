import { defineConfig } from "vitest/config";
import react from "@vitejs/plugin-react";
import { fileURLToPath, URL } from "node:url";

export default defineConfig({
    plugins: [react()],
    base: "/",
    resolve: {
        alias: {
            "@": fileURLToPath(new URL("./src", import.meta.url)),
        },
    },
    server: {
        // Porta FIXA em 5174 (a 5173 fica com outro projeto desta maquina). O Google exige
        // que a origem autorizada bata EXATAMENTE, entao porta que muda sozinha inviabiliza
        // o login social. strictPort faz falhar alto em vez de migrar para outra porta calado.
        // PORT ainda vence, para o caso de o container impor a porta.
        port: Number(process.env.PORT) || 5174,
        strictPort: true,
        proxy: {
            // O front fala sempre com "/api" (mesma origem) — em dev o proxy
            // encaminha para a API .NET local. Nada de CORS no desenvolvimento.
            "/api": {
                target: "http://localhost:5080",
                changeOrigin: true,
                secure: false,
            },
        },
    },
    test: {
        globals: true,
        environment: "jsdom",
        setupFiles: "./src/tests/setup.js",
        css: false,
        include: ["src/**/*.{test,spec}.{js,jsx}"],
    },
});
