import Badge from "@/components/ui/Badge.jsx";
import { descrever } from "@/lib/statusAdmin.js";

/**
 * Badge que traduz o valor cru do backend no rótulo acentuado e na variante de
 * cor certa. Recebe o MAPA (`STATUS_PEDIDO`, `STATUS_ENVIO`, `TIPO_CUPOM`...)
 * para não existir um `switch` gigante repetido em cada tela.
 *
 * Valor desconhecido aparece cru, com variante neutra: é melhor ver um rótulo
 * estranho do que um traço que esconde divergência de contrato.
 */
export default function BadgeStatus({ mapa, valor, className = "" }) {
    const { rotulo, variante } = descrever(mapa, valor);

    return (
        <Badge variante={variante} className={className}>
            {rotulo}
        </Badge>
    );
}
