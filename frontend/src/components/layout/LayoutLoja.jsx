import { Outlet } from "react-router-dom";
import Header from "./Header.jsx";
import Footer from "./Footer.jsx";

/**
 * Shell da loja. Light-only por decisao de marca — nenhuma classe `dark:` aqui
 * nem em nenhum componente da vitrine.
 *
 * A Home usa o `<Outlet>` sem container para poder sangrar o hero ate a borda;
 * as demais paginas aplicam `.shell` internamente.
 */
export default function LayoutLoja() {
    return (
        <div className="flex min-h-screen flex-col bg-base-100 text-base-content">
            <a
                href="#conteudo"
                className="sr-only focus:not-sr-only focus:absolute focus:left-4 focus:top-4 focus:z-top focus:bg-ink focus:px-4 focus:py-2 focus:text-xs focus:uppercase focus:tracking-widest focus:text-bone"
            >
                Pular para o conteúdo
            </a>

            <Header />

            <main id="conteudo" className="flex-1">
                <Outlet />
            </main>

            <Footer />
        </div>
    );
}
