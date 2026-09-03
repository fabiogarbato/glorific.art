/**
 * Tema visual do glorific.art — ver `design-system.md`.
 *
 * Regra de marca: o fundo NUNCA e branco puro (#FFF so dentro de foto de produto).
 * O off-white `bone` e a superficie principal (base-100); `linen` e a faixa
 * alternada (base-200); `sand` sao as bordas (base-300).
 *
 * A loja e light-only por decisao de marca — `darkTheme` aponta para o proprio
 * tema claro, entao nenhum `prefers-color-scheme` troca a paleta.
 */
import daisyui from "daisyui";

// Paleta crua — tambem exposta em theme.extend.colors para uso direto
// (`bg-bone`, `text-ink-soft`, `border-sand`...).
const brand = {
    bone: "#F8F5F0", // fundo principal (o off-white)
    linen: "#F1ECE3", // superficie alternada, cards, faixas
    sand: "#E3DBCE", // bordas, divisores, skeleton
    taupe: "#B9AE9C", // texto desabilitado, placeholder
    ink: "#1C1A17", // texto principal (carvao quente, nunca #000)
    "ink-soft": "#57514A", // texto secundario
    olive: "#6B7256", // primaria — ramo de oliveira
    "olive-dp": "#515943", // hover/pressed da primaria
    brass: "#B08D57", // acento — dourado envelhecido
    clay: "#A9603F", // secundaria quente — badges de promocao
    success: "#5B7551",
    warning: "#C08A2E",
    danger: "#9B4A3C", // erro em terracota
};

/** @type {import('tailwindcss').Config} */
export default {
    content: ["./index.html", "./src/**/*.{js,jsx}"],
    theme: {
        extend: {
            colors: {
                ...brand,
                brand,
            },
            fontFamily: {
                sans: ["Inter", "system-ui", "-apple-system", "sans-serif"],
                display: ["'Cormorant Garamond'", "Georgia", "serif"],
                accent: ["Inter", "system-ui", "sans-serif"],
            },
            // Escala do design-system: 12 / 14 / 16 / 20 / 24 / 32 / 44 / 64.
            fontSize: {
                xs: ["0.75rem", { lineHeight: "1rem" }],
                sm: ["0.875rem", { lineHeight: "1.25rem" }],
                base: ["1rem", { lineHeight: "1.5rem" }],
                lg: ["1.25rem", { lineHeight: "1.75rem" }],
                xl: ["1.5rem", { lineHeight: "2rem" }],
                "2xl": ["2rem", { lineHeight: "2.375rem" }],
                "3xl": ["2.75rem", { lineHeight: "3rem" }],
                "4xl": ["4rem", { lineHeight: "4.25rem" }],
            },
            // Foto de moda e retrato, nunca quadrada. -> `aspect-product`
            aspectRatio: {
                product: "3 / 4",
            },
            // Largura unica do shell (header/main/footer).
            maxWidth: {
                shell: "1440px",
            },
            // Escala de empilhamento nomeada — usar em vez de z-[n] avulso.
            zIndex: {
                dropdown: "10",
                sticky: "20",
                header: "30",
                backdrop: "40",
                overlay: "50",
                top: "60",
                toast: "70",
            },
            letterSpacing: {
                widest: "0.18em",
            },
            keyframes: {
                "fade-up": {
                    "0%": { opacity: "0", transform: "translateY(8px)" },
                    "100%": { opacity: "1", transform: "translateY(0)" },
                },
            },
            animation: {
                "fade-up": "fade-up .5s ease-out both",
            },
        },
    },
    plugins: [daisyui],
    daisyui: {
        logs: false,
        themes: [
            {
                glorific: {
                    "color-scheme": "light",
                    primary: brand.olive,
                    "primary-content": brand.bone,
                    secondary: brand.clay,
                    "secondary-content": brand.bone,
                    accent: brand.brass,
                    "accent-content": brand.ink,
                    neutral: brand.ink,
                    "neutral-content": brand.bone,
                    "base-100": brand.bone, // superficie principal
                    "base-200": brand.linen, // faixa alternada / card
                    "base-300": brand.sand, // bordas e divisores
                    "base-content": brand.ink,
                    info: brand["ink-soft"],
                    "info-content": brand.bone,
                    success: brand.success,
                    "success-content": brand.bone,
                    warning: brand.warning,
                    "warning-content": brand.ink,
                    error: brand.danger,
                    "error-content": brand.bone,
                    // Editorial: canto praticamente reto.
                    "--rounded-box": "0.125rem",
                    "--rounded-btn": "0rem",
                    "--rounded-badge": "0rem",
                    "--tab-radius": "0rem",
                    "--border-btn": "1px",
                    "--animation-btn": "0.2s",
                },
            },
        ],
        // Sem dark mode: o off-white E a identidade da loja.
        darkTheme: "glorific",
    },
};
