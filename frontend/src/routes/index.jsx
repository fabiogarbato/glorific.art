import { Suspense } from "react";
import { Outlet, Route, Routes } from "react-router-dom";

import LayoutLoja from "@/components/layout/LayoutLoja.jsx";
import LayoutAdmin from "@/components/layout/LayoutAdmin.jsx";
import RotaPrivada from "./RotaPrivada.jsx";
import RotaAdmin from "./RotaAdmin.jsx";
import RotaPolicy from "./RotaPolicy.jsx";
import EsperandoSessao from "./EsperandoSessao.jsx";

import Home from "@/pages/Home.jsx";
import NaoEncontrado from "@/pages/NaoEncontrado.jsx";

import rotasAuth from "./rotasAuth.jsx";
import rotasVitrine from "./rotasVitrine.jsx";
import rotasInstitucional from "./rotasInstitucional.jsx";
import rotasCompra from "./rotasCompra.jsx";
import rotasAdminCatalogo from "./rotasAdminCatalogo.jsx";
import rotasAdminOperacao from "./rotasAdminOperacao.jsx";

/**
 * Mapa de rotas, composto a partir dos arrays de cada área
 * (`rotas<Area>.jsx`, no formato `{ path, element, publica?, policy? }`).
 *
 * A montagem é feita aqui, e não em cada arquivo, porque só neste ponto se
 * conhece o chassi e a guarda de cada grupo:
 *
 *   loja pública    -> LayoutLoja
 *   loja autenticada-> LayoutLoja + RotaPrivada
 *   painel          -> RotaAdmin + LayoutAdmin + RotaPolicy por destino
 *
 * As telas do painel são carregadas sob demanda (`React.lazy` nos arrays de
 * admin): quem só visita a vitrine não baixa o painel inteiro junto. O
 * `<Suspense>` correspondente fica dentro do `LayoutAdmin`, para a barra
 * lateral continuar na tela enquanto o pedaço chega.
 */

/** Divide um array de rotas entre públicas e privadas. */
function separarPorAcesso(rotas) {
    return {
        publicas: rotas.filter((r) => r.publica),
        privadas: rotas.filter((r) => !r.publica),
    };
}

const compra = separarPorAcesso(rotasCompra);

/**
 * Rotas do painel, na ordem em que devem ser declaradas.
 *
 * `/admin` sai da lista: ele é a rota índice do grupo e é declarado à parte
 * (`<Route index>`), senão o mesmo caminho apareceria duas vezes.
 */
const rotasAdmin = [...rotasAdminOperacao, ...rotasAdminCatalogo].filter(
    (r) => r.path !== "/admin",
);

const rotaIndiceAdmin = rotasAdminOperacao.find((r) => r.path === "/admin");

/**
 * Espera do pedaço carregado sob demanda. Reaproveita o esqueleto da sessão em
 * vez de um spinner: a janela é curta e a página inteira já tem o chassi.
 */
function AguardandoTela() {
    return (
        <Suspense fallback={<EsperandoSessao />}>
            <Outlet />
        </Suspense>
    );
}

export default function AppRoutes() {
    return (
        <Routes>
            {/* ------------------------------------------------ LOJA (público) */}
            <Route element={<LayoutLoja />}>
                <Route index element={<Home />} />

                {/* Sessão: entrar, criar conta e recuperar senha. */}
                {rotasAuth.map((rota) => (
                    <Route key={rota.path} path={rota.path} element={rota.element} />
                ))}

                {/* Vitrine: catálogo, busca, categoria, coleções e produto. */}
                {rotasVitrine.map((rota) => (
                    <Route key={rota.path} path={rota.path} element={rota.element} />
                ))}

                {/* Compra sem sessão: a sacola é montada antes de haver conta. */}
                {compra.publicas.map((rota) => (
                    <Route key={rota.path} path={rota.path} element={rota.element} />
                ))}

                {/*
                 * Institucional: sobre a marca, guia de medidas e as políticas.
                 * São os endereços que Header e Footer linkam, e os que o robô
                 * de busca lê — por isso públicos, junto da vitrine. Um slug de
                 * política desconhecido cai no 404 decidido dentro da própria
                 * page, não numa rota curinga a mais.
                 */}
                {rotasInstitucional.map((rota) => (
                    <Route key={rota.path} path={rota.path} element={rota.element} />
                ))}

                {/* ------------------------------------------ LOJA (autenticado) */}
                <Route element={<RotaPrivada />}>
                    {compra.privadas.map((rota) => (
                        <Route key={rota.path} path={rota.path} element={rota.element} />
                    ))}
                </Route>

                <Route path="*" element={<NaoEncontrado />} />
            </Route>

            {/* ------------------------------------------------------- PAINEL */}
            <Route element={<RotaAdmin />}>
                <Route path="/admin" element={<LayoutAdmin />}>
                    <Route element={<AguardandoTela />}>
                        {rotaIndiceAdmin && (
                            <Route
                                index
                                element={
                                    <RotaPolicy policy={rotaIndiceAdmin.policy}>
                                        {rotaIndiceAdmin.element}
                                    </RotaPolicy>
                                }
                            />
                        )}

                        {rotasAdmin.map((rota) => (
                            <Route
                                key={rota.path}
                                path={rota.path}
                                element={
                                    rota.policy ? (
                                        <RotaPolicy policy={rota.policy}>
                                            {rota.element}
                                        </RotaPolicy>
                                    ) : (
                                        rota.element
                                    )
                                }
                            />
                        ))}

                        <Route path="*" element={<NaoEncontrado />} />
                    </Route>
                </Route>
            </Route>
        </Routes>
    );
}
