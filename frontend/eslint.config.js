import js from "@eslint/js";
import globals from "globals";
import reactHooks from "eslint-plugin-react-hooks";
import reactRefresh from "eslint-plugin-react-refresh";
import { defineConfig, globalIgnores } from "eslint/config";

export default defineConfig([
    globalIgnores(["dist"]),
    {
        files: ["**/*.{js,jsx}"],
        extends: [
            js.configs.recommended,
            reactHooks.configs["recommended-latest"],
            reactRefresh.configs.vite,
        ],
        languageOptions: {
            ecmaVersion: 2022,
            globals: { ...globals.browser, ...globals.node },
            parserOptions: {
                ecmaVersion: "latest",
                ecmaFeatures: { jsx: true },
                sourceType: "module",
            },
        },
        rules: {
            // Sem eslint-plugin-react, o uso de um identificador em JSX nao conta
            // como leitura — por isso nomes em PascalCase/CAPS (componentes)
            // ficam de fora da regra, tanto em import quanto em destructuring.
            "no-unused-vars": [
                "error",
                { varsIgnorePattern: "^[A-Z_]", argsIgnorePattern: "^[A-Z_]|^_" },
            ],
            // Contexto e helper convivendo com componente no mesmo arquivo e
            // decisao consciente (ver contexts/* e ui/Paginacao.jsx): custa um
            // full reload no HMR, nao quebra build.
            "react-refresh/only-export-components": ["warn", { allowConstantExport: true }],
        },
    },
    {
        files: ["src/tests/**/*.{js,jsx}", "**/*.{test,spec}.{js,jsx}"],
        languageOptions: { globals: { ...globals.browser, ...globals.node } },
    },
]);
