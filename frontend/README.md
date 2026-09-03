# glorific.art — frontend

Vitrine e painel administrativo do ecommerce de moda crista **glorific.art**.
React 19 + Vite 6 + Tailwind 3 + DaisyUI 4 + TanStack Query v5 + axios + React Router v7.

## Comandos

```bash
npm run dev      # servidor de desenvolvimento (proxy /api -> localhost:5080)
npm run build    # build de producao em dist/
npm run preview  # serve o dist/
npm run lint     # eslint
npm test         # vitest (uma passada)
```

Copie `.env.example` para `.env` antes do primeiro `npm run dev`.

## Regras de arquitetura (nao negociaveis)

1. **Zero `fetch` cru.** Toda chamada de rede passa por `src/api/client.js` — a
   unica instancia de axios do projeto, com os dois interceptors (Bearer + exp
   do JWT na request; 401/404/toast na response).
2. **A page nunca importa axios nem service.** A cadeia e sempre
   `page -> hook -> service -> api/client`.
3. **Chaves de query centralizadas** em `src/lib/queryKeys.js`. Nada de array
   literal solto no `useQuery`.
4. **Dinheiro em centavos** (inteiro), formatado por
   `utils/financeiro.formatarCentavosParaBRL`.
5. **A loja e light-only.** Nenhuma classe `dark:` na vitrine — o off-white e a
   identidade da marca (ver `design-system.md`).

## Sistema visual

Tema DaisyUI unico chamado `glorific`, fixado em `<html data-theme="glorific">`.
Paleta: `bone` (fundo), `linen` (faixa), `sand` (borda), `taupe` (placeholder),
`ink`/`ink-soft` (texto), `olive`/`olive-dp` (primaria), `brass` (acento),
`clay` (secundaria quente). Titulos em Cormorant Garamond, corpo em Inter,
preco com `tabular-nums`. Foto de produto sempre `aspect-product` (3:4).
