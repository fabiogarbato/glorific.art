import Modal from "./Modal.jsx";
import Botao from "./Botao.jsx";

/**
 * Overlay generico de confirmacao — base de toda acao destrutiva.
 *
 * Uso tipico com estado "objeto ou null":
 *   const [confirmar, setConfirmar] = useState(null);
 *   <ConfirmModal isOpen={!!confirmar} ... onCancel={() => setConfirmar(null)} />
 */
export default function ConfirmModal({
    isOpen,
    titulo = "Confirmar ação",
    mensagem,
    onConfirm,
    onCancel,
    textoConfirmar = "Excluir",
    textoCancelar = "Cancelar",
    variante = "perigo",
    carregando = false,
}) {
    return (
        <Modal
            isOpen={isOpen}
            onClose={onCancel}
            titulo={titulo}
            largura="sm"
            rodape={
                <>
                    <Botao variante="contorno" onClick={onCancel} disabled={carregando}>
                        {textoCancelar}
                    </Botao>
                    <Botao variante={variante} onClick={onConfirm} carregando={carregando}>
                        {textoConfirmar}
                    </Botao>
                </>
            }
        >
            <p className="text-base leading-relaxed">{mensagem}</p>
        </Modal>
    );
}
