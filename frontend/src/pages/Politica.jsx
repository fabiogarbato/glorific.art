/**
 * AVISO AO LOJISTA — LEIA ANTES DE PUBLICAR.
 *
 * O texto desta pagina e um PADRAO DE LOJA escrito em linguagem clara, para
 * servir de ponto de partida. Ele NAO e parecer juridico e nao substitui a
 * revisao de um advogado: razao social, CNPJ, prazos, foro e o tratamento de
 * dados pessoais precisam ser conferidos contra a operacao real da loja antes
 * de ir ao ar.
 *
 * Nada aqui afirma certificacao, selo de seguranca, auditoria ou garantia que a
 * loja nao possua — se um dia existir, entra com nome e numero.
 *
 * Os numeros que a loja configura (prazo de troca, prazo de manuseio, valor do
 * frete cortesia) ficam TODOS no bloco `CONFIG_LOJA` logo abaixo: e o unico
 * ponto do arquivo a mexer quando a regra comercial mudar. Nao ha rota publica
 * de configuracao no backend hoje (`/admin/configuracoes` exige policy de
 * administrador), entao o valor e constante de propria vontade — quando o
 * endpoint publico existir, basta trocar este bloco por um hook.
 */
import { Link, useParams } from "react-router-dom";

import Botao from "@/components/ui/Botao.jsx";
import NaoEncontrado from "@/pages/NaoEncontrado.jsx";
import { STORE } from "@/data/store.js";
import { formatarCentavosParaBRL } from "@/utils/financeiro.js";

// ---------------------------------------------------------------------------
// PONTO UNICO DE AJUSTE dos valores vigentes da loja.
// Espelha `ConfiguracaoLoja` no backend; mantenha os dois iguais.
// ---------------------------------------------------------------------------
const CONFIG_LOJA = {
    /** Janela de troca por arrependimento/tamanho, em dias corridos. */
    prazoTrocaDias: 30,
    /** Dias uteis entre o pagamento aprovado e a postagem. */
    prazoManuseioDias: 2,
    /** Frete cortesia a partir deste valor, em CENTAVOS. `null` desliga a regra. */
    freteGratisAcimaDeCentavos: 39900,
    /**
     * Data de vigencia mostrada no topo da politica ("Vigente desde").
     * Deixe `null` ate a versao revisada ir ao ar — data inventada em documento
     * juridico e pior do que data nenhuma.
     */
    vigenteDesde: null,
};

const PRAZO_TROCA = `${CONFIG_LOJA.prazoTrocaDias} dias`;
const PRAZO_MANUSEIO = `${CONFIG_LOJA.prazoManuseioDias} dias úteis`;
const FRETE_CORTESIA =
    CONFIG_LOJA.freteGratisAcimaDeCentavos !== null
        ? formatarCentavosParaBRL(CONFIG_LOJA.freteGratisAcimaDeCentavos)
        : null;

const EMAIL = STORE.contact.email;

// ---------------------------------------------------------------------------
// Conteudo. Cada politica e `{ titulo, resumo, secoes: [{ titulo, paragrafos,
// itens }] }` — a page so sabe desenhar essa forma, entao acrescentar uma nova
// politica e acrescentar uma chave aqui.
// ---------------------------------------------------------------------------
const POLITICAS = {
    trocas: {
        titulo: "Trocas e devoluções",
        resumo:
            "O que fazer quando a peça não serviu, não era o que você esperava ou chegou com defeito.",
        secoes: [
            {
                titulo: "Arrependimento: 7 dias garantidos por lei",
                paragrafos: [
                    "Como esta é uma compra feita fora de loja física, você tem o direito de desistir dela em até 7 dias corridos contados do recebimento da encomenda, sem precisar justificar o motivo. Esse prazo está no artigo 49 do Código de Defesa do Consumidor e vale mesmo que a peça esteja perfeita e sirva em você.",
                    "Nesse caso devolvemos tudo o que foi pago, inclusive o frete, pelo mesmo meio de pagamento usado na compra. O custo de devolver a peça para nós é nosso.",
                ],
            },
            {
                titulo: `Troca por tamanho ou cor: ${PRAZO_TROCA}`,
                paragrafos: [
                    `Além do prazo legal, oferecemos por conta própria uma janela de ${PRAZO_TROCA}, contados do recebimento, para trocar a peça por outro tamanho ou por outra cor da mesma referência. Esta é uma condição comercial nossa: é o prazo vigente hoje e vale para o pedido feito enquanto ele estiver publicado aqui.`,
                    "A troca depende de a peça estar disponível em estoque. Se não estiver, você escolhe entre outra peça de valor equivalente, crédito na loja ou a devolução do valor pago.",
                ],
            },
            {
                titulo: "Em que estado a peça precisa voltar",
                paragrafos: [
                    "Para troca e devolução, a peça precisa voltar sem sinais de uso, sem odor, sem alteração de costura ou barra, com as etiquetas presas e na embalagem original sempre que possível. Provar em casa é esperado e não conta como uso.",
                ],
                itens: [
                    "Não trocamos peça lavada, ajustada, customizada ou com marca de maquiagem, perfume ou desodorante.",
                    "Peça de banho e roupa íntima, quando houver, só voltam com o lacre de higiene intacto.",
                    "Peça comprada em condição de ponta de estoque continua trocável dentro do prazo legal de 7 dias.",
                ],
            },
            {
                titulo: "Defeito de fabricação",
                paragrafos: [
                    "Se a peça chegou com defeito ou apresentou defeito de fabricação com o uso normal, o prazo para reclamar é de 30 dias corridos do recebimento, por se tratar de produto não durável (artigo 26 do Código de Defesa do Consumidor). Assumimos o frete nos dois sentidos e você escolhe entre reparo, troca por peça igual ou devolução integral do valor.",
                    "Desgaste natural de uso, dano por lavagem fora do que diz a etiqueta e rasgo por acidente não são defeito de fabricação.",
                ],
            },
            {
                titulo: "Como pedir",
                paragrafos: [
                    `Escreva para ${EMAIL} com o número do pedido, a peça envolvida e, quando houver defeito, uma foto que mostre o problema. Respondemos com a etiqueta de postagem ou com a orientação de envio e acompanhamos até o fim.`,
                    "O valor da devolução é processado depois que a peça chega e é conferida por aqui. O prazo de retorno na fatura do cartão depende do banco emissor e costuma cair em uma ou duas faturas seguintes; em Pix, devolvemos na conta de origem.",
                ],
            },
        ],
    },

    entrega: {
        titulo: "Prazos e entrega",
        resumo: "Como calculamos o prazo, quando a peça é postada e o que fazer se ela atrasar.",
        secoes: [
            {
                titulo: "Como o prazo é formado",
                paragrafos: [
                    `O prazo que aparece no checkout é a soma de duas partes: o tempo de preparo do pedido aqui (hoje, até ${PRAZO_MANUSEIO} após a confirmação do pagamento) e o prazo da transportadora escolhida, que depende do CEP de destino.`,
                    "O relógio só começa a correr quando o pagamento é confirmado. Em cartão isso costuma ser imediato; em Pix, poucos minutos; em boleto, pode levar até três dias úteis para a compensação aparecer para nós.",
                ],
            },
            {
                titulo: "Frete",
                paragrafos: [
                    FRETE_CORTESIA
                        ? `O valor do frete é calculado pelo CEP na página do produto e no carrinho, antes de você pagar. Pedidos a partir de ${FRETE_CORTESIA} têm frete cortesia — esse é o valor vigente hoje e pode mudar em campanhas futuras; vale sempre o que estiver na tela no momento da compra.`
                        : "O valor do frete é calculado pelo CEP na página do produto e no carrinho, antes de você pagar. Vale sempre o valor que estiver na tela no momento da compra.",
                ],
            },
            {
                titulo: "Acompanhamento",
                paragrafos: [
                    "Assim que a encomenda é postada, o código de rastreio aparece na página do pedido, dentro da sua conta, e também é enviado por e-mail. O rastreio pode levar algumas horas para mostrar o primeiro movimento depois da postagem.",
                ],
            },
            {
                titulo: "Endereço, ausência e extravio",
                paragrafos: [
                    "Confira o endereço antes de finalizar: endereço incompleto é a causa mais comum de devolução ao remetente. Se o pedido voltar por endereço incorreto informado na compra, combinamos o reenvio e o novo frete fica por sua conta.",
                    "Na ausência de alguém para receber, a transportadora costuma tentar a entrega mais de uma vez antes de devolver o pacote. Se o prazo estourar ou a encomenda parar de se mover no rastreio, escreva para " +
                        EMAIL +
                        " que abrimos a busca junto à transportadora e mantemos você informada do andamento.",
                ],
            },
        ],
    },

    privacidade: {
        titulo: "Política de privacidade",
        resumo:
            "Quais dados pessoais tratamos, por que precisamos deles e como você exerce os seus direitos.",
        secoes: [
            {
                titulo: "Quem trata os seus dados",
                paragrafos: [
                    `Os dados coletados neste site são tratados por ${STORE.legalName}, na condição de controladora, nos termos da Lei Geral de Proteção de Dados (Lei 13.709/2018). Para qualquer assunto relacionado a dados pessoais, o canal é ${EMAIL}.`,
                ],
            },
            {
                titulo: "O que coletamos",
                paragrafos: ["Coletamos apenas o necessário para vender e entregar:"],
                itens: [
                    "Cadastro: nome, e-mail, telefone e senha (guardada apenas como código embaralhado, nunca em texto legível).",
                    "Compra: endereço de entrega, CPF quando exigido para emissão da nota fiscal, itens do pedido e histórico de pedidos.",
                    "Pagamento: os dados do cartão são digitados no ambiente do meio de pagamento e não trafegam nem ficam guardados nos nossos servidores. Recebemos de volta apenas o resultado da transação e os últimos dígitos do cartão.",
                    "Navegação: dados técnicos como endereço IP, tipo de navegador e páginas visitadas, usados para manter o site no ar e entender o que precisa melhorar.",
                ],
            },
            {
                titulo: "Por que tratamos cada dado",
                paragrafos: [
                    "Cada tratamento tem uma base legal: executar o contrato de compra e venda (processar o pedido, entregar, dar suporte e cuidar de trocas), cumprir obrigação legal e regulatória (emissão fiscal e guarda de documentos pelos prazos exigidos), e legítimo interesse (segurança do site, prevenção a fraude e melhoria da loja).",
                    "E-mail de novidades só é enviado com o seu consentimento, dado ao se inscrever. Ele pode ser retirado a qualquer momento pelo link de descadastro no rodapé da mensagem ou pelo e-mail de contato, sem prejuízo das mensagens sobre pedidos em andamento.",
                ],
            },
            {
                titulo: "Com quem compartilhamos",
                paragrafos: [
                    "Compartilhamos o mínimo, e só com quem participa da operação: o meio de pagamento (para autorizar a cobrança), a transportadora (nome e endereço, para entregar), o serviço de emissão de nota fiscal e os prestadores de tecnologia que hospedam a loja e enviam nossos e-mails.",
                    "Não vendemos dados pessoais e não os cedemos para publicidade de terceiros. Fora isso, só entregamos dados mediante ordem de autoridade competente.",
                ],
            },
            {
                titulo: "Por quanto tempo guardamos",
                paragrafos: [
                    "Dados de cadastro ficam enquanto a sua conta existir. Dados de pedidos e documentos fiscais são guardados pelos prazos que a legislação fiscal e civil exige, mesmo depois do encerramento da conta, e depois são eliminados ou anonimizados.",
                ],
            },
            {
                titulo: "Cookies",
                paragrafos: [
                    "Usamos cookies necessários para o funcionamento do site: manter a sua sessão iniciada e lembrar o que está na sacola. Sem eles a loja não funciona. Cookies de medição de audiência, quando houver, são informados no aviso de cookies e podem ser recusados sem perder o acesso à loja. O seu navegador também permite bloquear e apagar cookies a qualquer momento.",
                ],
            },
            {
                titulo: "Os seus direitos",
                paragrafos: [
                    "A LGPD garante que você peça, a qualquer momento: confirmação de que tratamos dados seus, acesso a esses dados, correção do que estiver errado, anonimização ou eliminação do que for desnecessário, portabilidade, informação sobre com quem compartilhamos e revogação do consentimento.",
                    `Para exercer qualquer um deles, escreva para ${EMAIL}. Respondemos no menor prazo possível e, no máximo, dentro do prazo legal. Podemos pedir alguma informação a mais só para confirmar que é você mesmo — pedido de dados vindo de outra pessoa é o risco que essa checagem evita.`,
                ],
            },
            {
                titulo: "Segurança",
                paragrafos: [
                    "Adotamos medidas técnicas e administrativas razoáveis para proteger os dados: conexão criptografada, senha guardada em formato irreversível, acesso restrito à equipe que precisa dele e registro das operações administrativas. Nenhum sistema é imune a incidentes; se algum ocorrer com risco relevante, comunicamos as pessoas afetadas e a Autoridade Nacional de Proteção de Dados, como manda a lei.",
                ],
            },
        ],
    },

    termos: {
        titulo: "Termos de uso",
        resumo: "As regras de uso deste site e as condições da compra feita por aqui.",
        secoes: [
            {
                titulo: "Aceite",
                paragrafos: [
                    `Ao navegar, criar conta ou comprar neste site, você concorda com estes termos e com a política de privacidade. A loja é operada por ${STORE.legalName}. Se alguma condição aqui não fizer sentido para você, o melhor caminho é falar com a gente antes de comprar.`,
                ],
            },
            {
                titulo: "Sua conta",
                paragrafos: [
                    "A conta é pessoal e a senha é sua responsabilidade: não compartilhe. Os dados informados no cadastro precisam ser verdadeiros e atualizados — endereço errado atrasa entrega e CPF incorreto impede a emissão da nota fiscal.",
                    "Podemos suspender uma conta usada para fraude, revenda não autorizada, tentativa de invasão ou qualquer uso que prejudique outras pessoas.",
                ],
            },
            {
                titulo: "Produtos, preço e disponibilidade",
                paragrafos: [
                    "Fotografamos as peças com a maior fidelidade possível, mas a cor pode variar conforme a tela do seu aparelho. Medidas, composição e cuidados estão na ficha de cada peça e no guia de medidas.",
                    "Preços e condições valem para compras feitas neste site e podem mudar sem aviso; o que vale é o preço mostrado no momento em que você fecha o pedido. Trabalhamos com lotes pequenos: um item pode esgotar entre a sua visita e o pagamento.",
                    "Se um erro evidente de sistema publicar um preço claramente incorreto, entramos em contato antes de qualquer cobrança e você decide se quer seguir pelo valor correto ou cancelar sem custo.",
                ],
            },
            {
                titulo: "Pedido e pagamento",
                paragrafos: [
                    "O pedido só se confirma quando o pagamento é aprovado pelo meio de pagamento. Antes disso, ele fica aguardando e o estoque não está garantido. Podemos recusar um pedido com suspeita de fraude, com dados inconsistentes ou com quantidade incompatível com consumo pessoal.",
                ],
            },
            {
                titulo: "Conteúdo do site",
                paragrafos: [
                    "Textos, fotos, ilustrações, marca e o desenho das peças são de titularidade da loja ou de quem nos licenciou. Você pode compartilhar links e imagens do site nas suas redes, com crédito. Uso comercial, cópia do catálogo ou coleta automatizada de conteúdo dependem de autorização por escrito.",
                ],
            },
            {
                titulo: "Limites e alterações",
                paragrafos: [
                    "Fazemos o possível para manter a loja no ar e as informações corretas, mas não prometemos funcionamento sem qualquer interrupção — manutenção e falhas de terceiros acontecem. Nada nestes termos afasta os direitos que o Código de Defesa do Consumidor garante a você.",
                    "Estes termos podem ser atualizados. A versão publicada nesta página é a que vale, e a compra fica regida pela versão vigente na data em que foi feita.",
                ],
            },
            {
                titulo: "Lei aplicável",
                paragrafos: [
                    "Aplica-se a legislação brasileira. Questões de consumo podem ser levadas ao foro do domicílio do consumidor, como assegura o Código de Defesa do Consumidor. Antes disso, escreva para " +
                        EMAIL +
                        ": a maior parte dos problemas se resolve na conversa.",
                ],
            },
        ],
    },
};

/**
 * Apelidos de endereco. Enderecos antigos e variacoes obvias precisam continuar
 * abrindo a pagina certa — link de rodape indexado que vira 404 e perda boba.
 */
const APELIDOS = {
    "trocas-e-devolucoes": "trocas",
    devolucoes: "trocas",
    troca: "trocas",
    envio: "entrega",
    frete: "entrega",
    "prazos-e-entrega": "entrega",
    "politica-de-privacidade": "privacidade",
    "termos-de-uso": "termos",
    "termos-e-condicoes": "termos",
};

/** Menu de rodape da propria pagina: as quatro politicas, na ordem de leitura. */
const INDICE = [
    { slug: "trocas", rotulo: "Trocas e devoluções" },
    { slug: "entrega", rotulo: "Prazos e entrega" },
    { slug: "privacidade", rotulo: "Privacidade" },
    { slug: "termos", rotulo: "Termos de uso" },
];

export default function Politica() {
    const { slug } = useParams();
    const chave = APELIDOS[String(slug ?? "").toLowerCase()] ?? String(slug ?? "").toLowerCase();
    const politica = POLITICAS[chave];

    // Slug que nao existe e endereco inexistente — devolve a MESMA tela de 404 da
    // loja, com o status certo aos olhos de quem navega, e nao um bloco vazio.
    if (!politica) return <NaoEncontrado />;

    return (
        <div className="animate-fade-up">
            <div className="shell grid gap-12 py-12 lg:grid-cols-12 lg:py-16">
                {/* ------------------------------------------------- SUMARIO */}
                <aside className="lg:col-span-3">
                    <nav aria-label="Políticas da loja" className="lg:sticky lg:top-28">
                        <p className="eyebrow">Políticas</p>
                        <ul className="mt-5 flex flex-col gap-3">
                            {INDICE.map((item) => {
                                const atual = item.slug === chave;
                                return (
                                    <li key={item.slug}>
                                        <Link
                                            to={`/politicas/${item.slug}`}
                                            aria-current={atual ? "page" : undefined}
                                            className={`text-sm transition-colors ${
                                                atual
                                                    ? "text-ink underline underline-offset-4 decoration-brass"
                                                    : "text-ink-soft hover:text-ink"
                                            }`}
                                        >
                                            {item.rotulo}
                                        </Link>
                                    </li>
                                );
                            })}
                        </ul>
                    </nav>
                </aside>

                {/* -------------------------------------------------- TEXTO */}
                <article className="lg:col-span-8 lg:col-start-5">
                    <header>
                        <p className="eyebrow">Institucional</p>
                        <h1 className="mt-4 font-display text-2xl tracking-tight text-ink sm:text-3xl">
                            {politica.titulo}
                        </h1>
                        <p className="mt-5 max-w-2xl text-base leading-relaxed text-ink-soft">
                            {politica.resumo}
                        </p>
                        {CONFIG_LOJA.vigenteDesde && (
                            <p className="mt-4 text-xs uppercase tracking-widest text-taupe">
                                Vigente desde {CONFIG_LOJA.vigenteDesde}
                            </p>
                        )}
                        <div className="filete mt-8" />
                    </header>

                    <div className="mt-10 flex flex-col gap-10">
                        {politica.secoes.map((secao) => (
                            <section key={secao.titulo}>
                                <h2 className="font-display text-xl tracking-tight text-ink">
                                    {secao.titulo}
                                </h2>

                                {secao.paragrafos?.map((paragrafo) => (
                                    <p
                                        key={paragrafo.slice(0, 40)}
                                        className="mt-4 max-w-2xl text-base leading-relaxed text-ink-soft"
                                    >
                                        {paragrafo}
                                    </p>
                                ))}

                                {secao.itens && (
                                    <ul className="mt-5 flex max-w-2xl flex-col gap-3">
                                        {secao.itens.map((item) => (
                                            <li
                                                key={item.slice(0, 40)}
                                                className="flex gap-3 text-sm leading-relaxed text-ink-soft"
                                            >
                                                <span aria-hidden="true" className="text-brass">
                                                    ✦
                                                </span>
                                                <span>{item}</span>
                                            </li>
                                        ))}
                                    </ul>
                                )}
                            </section>
                        ))}
                    </div>

                    {/* ---------------------------------------------- CONTATO */}
                    <div className="mt-16 border border-sand bg-linen px-6 py-10">
                        <h2 className="font-display text-xl tracking-tight text-ink">
                            Ficou alguma dúvida?
                        </h2>
                        <p className="mt-3 max-w-xl text-sm leading-relaxed text-ink-soft">
                            Fale com a gente antes de decidir. Respondemos em português claro, com
                            o nome de quem está do outro lado.
                        </p>
                        <div className="mt-6 flex flex-wrap gap-4">
                            <Botao href={`mailto:${EMAIL}`} variante="contorno">
                                Escrever para a loja
                            </Botao>
                            <Botao to="/guia-de-medidas" variante="texto">
                                Ver o guia de medidas
                            </Botao>
                        </div>
                    </div>
                </article>
            </div>
        </div>
    );
}
