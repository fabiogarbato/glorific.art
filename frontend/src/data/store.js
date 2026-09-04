/** Dados de marca e conteudo estatico. Nada aqui vem da API. */
export const STORE = Object.freeze({
    name: "glorific.art",
    legalName: "Glorific Art Comércio de Vestuário Ltda.",
    tagline: "A arte de glorificar",
    manifesto:
        "Camisetas oversized, streetwear e fé sem meio-termo. Peças de rua com propósito: o sagrado na estampa, não na etiqueta.",

    contact: {
        email: "contato@glorific.art",
        phone: "(41) 90000-0000",
    },

    social: {
        instagram: "https://instagram.com/art.glorific",
        whatsapp: "5541900000000",
        whatsapp_text: "Olá! Vim pelo site da glorific.art e gostaria de ajuda.",
    },

    navegacao: [
        { label: "Novidades", to: "/catalogo?ordem=recentes" },
        { label: "Camisetas", to: "/categoria/camisetas" },
        { label: "Coleções", to: "/colecoes" },
        { label: "Sobre", to: "/sobre" },
    ],

    institucional: [
        { label: "Sobre a marca", to: "/sobre" },
        { label: "Guia de medidas", to: "/guia-de-medidas" },
        { label: "Trocas e devoluções", to: "/politicas/trocas" },
        { label: "Privacidade", to: "/politicas/privacidade" },
    ],
});

export default STORE;
