import AppRoutes from "@/routes/index.jsx";

/**
 * O App e so o mapa de rotas — cada grupo traz o proprio chassi
 * (`LayoutLoja` / `LayoutAdmin`). Providers ficam no `main.jsx`.
 */
export default function App() {
    return <AppRoutes />;
}
