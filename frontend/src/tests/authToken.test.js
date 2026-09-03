import { describe, expect, it } from "vitest";
import { lerPayloadJwt, tokenValido } from "@/api/client.js";
import {
    ehAdministrativo,
    extrairPapeisDoPayload,
    extrairUsuarioDoToken,
    montarUsuario,
} from "@/contexts/AuthContext.jsx";
import { CLAIM } from "@/lib/constants.js";

/**
 * Decodificacao do JWT emitido por Infrastructure/Security/TokenService.
 *
 * O que esta sendo protegido aqui: o servidor limpa o OutboundClaimTypeMap e
 * escreve claims curtas (sub, email, name, role). Se alguem "melhorar" o leitor
 * para procurar nameidentifier de novo, o front volta a achar que todo mundo e
 * cliente anonimo — que foi o bug do repo de referencia.
 */

/** base64url sem padding, exatamente como o JWT trafega. */
function base64url(objeto) {
    const bytes = new TextEncoder().encode(JSON.stringify(objeto));
    const binario = String.fromCharCode(...bytes);
    return btoa(binario).replace(/\+/g, "-").replace(/\//g, "_").replace(/=+$/, "");
}

function montarJwt(payload) {
    return [base64url({ alg: "HS256", typ: "JWT" }), base64url(payload), "assinatura-falsa"].join(
        ".",
    );
}

const daquiA = (segundos) => Math.floor(Date.now() / 1000) + segundos;

describe("decodificacao do JWT", () => {
    it("le o payload mesmo sem padding e com acento no nome", () => {
        const token = montarJwt({ sub: "u-1", name: "Ana Conceição", email: "ana@teste.com" });

        expect(lerPayloadJwt(token)).toMatchObject({
            sub: "u-1",
            name: "Ana Conceição",
            email: "ana@teste.com",
        });
    });

    it("devolve null para token quebrado, vazio ou sem as tres partes", () => {
        expect(lerPayloadJwt("")).toBeNull();
        expect(lerPayloadJwt("sem-ponto-nenhum")).toBeNull();
        expect(lerPayloadJwt("a.b")).toBeNull();
        expect(lerPayloadJwt("a.###.c")).toBeNull();
    });

    it("tokenValido olha o exp, com folga para latencia", () => {
        expect(tokenValido(montarJwt({ exp: daquiA(900) }))).toBe(true);
        expect(tokenValido(montarJwt({ exp: daquiA(-1) }))).toBe(false);
        // Sem exp nao ha como afirmar que vale.
        expect(tokenValido(montarJwt({ sub: "u-1" }))).toBe(false);
        expect(tokenValido(null)).toBe(false);
    });
});

describe("papeis do payload", () => {
    it("aceita um papel so como string", () => {
        expect(extrairPapeisDoPayload({ role: "gerente" })).toEqual(["gerente"]);
    });

    it("aceita varios papeis como array e normaliza caixa e espaco", () => {
        expect(extrairPapeisDoPayload({ role: ["Admin", " Operador ", "cliente"] })).toEqual([
            "admin",
            "operador",
            "cliente",
        ]);
    });

    it("ainda entende a URI longa de schema do .NET", () => {
        expect(extrairPapeisDoPayload({ [CLAIM.role]: "admin" })).toEqual(["admin"]);
    });

    it("devolve lista vazia quando nao ha papel algum", () => {
        expect(extrairPapeisDoPayload({})).toEqual([]);
        expect(extrairPapeisDoPayload(null)).toEqual([]);
        expect(extrairPapeisDoPayload({ role: ["", null, 7] })).toEqual([]);
    });
});

describe("usuario a partir do token", () => {
    it("usa sub como identidade publica e nunca inventa id inteiro", () => {
        const usuario = extrairUsuarioDoToken(
            montarJwt({ sub: "uuid-abc", email: "ana@teste.com", name: "Ana", role: "cliente" }),
        );

        expect(usuario.uuid).toBe("uuid-abc");
        expect(usuario.email).toBe("ana@teste.com");
        expect(usuario.nome).toBe("Ana");
        expect(usuario.role).toBe("cliente");
        expect(usuario.isAdmin).toBe(false);
    });

    it("trata token sem papel como cliente", () => {
        const usuario = extrairUsuarioDoToken(montarJwt({ sub: "uuid-abc" }));

        expect(usuario.papeis).toEqual([]);
        expect(usuario.role).toBe("cliente");
        expect(usuario.isAdmin).toBe(false);
    });

    it("devolve null para token ausente ou ilegivel", () => {
        expect(extrairUsuarioDoToken(null)).toBeNull();
        expect(extrairUsuarioDoToken("lixo")).toBeNull();
    });
});

describe("papel administrativo", () => {
    it("reconhece os tres papeis do painel", () => {
        expect(ehAdministrativo(["admin"])).toBe(true);
        expect(ehAdministrativo(["gerente"])).toBe(true);
        expect(ehAdministrativo(["operador"])).toBe(true);
        // Papel administrativo em segunda posicao continua valendo.
        expect(ehAdministrativo(["cliente", "operador"])).toBe(true);
    });

    it("recusa cliente e lista vazia", () => {
        expect(ehAdministrativo(["cliente"])).toBe(false);
        expect(ehAdministrativo([])).toBe(false);
        expect(ehAdministrativo(undefined)).toBe(false);
    });
});

describe("montagem do usuario da interface", () => {
    const token = montarJwt({ sub: "uuid-abc", email: "ana@teste.com", role: "cliente" });

    it("prefere o perfil do backend ao que esta no token", () => {
        const usuario = montarUsuario(
            {
                id: 7,
                uuid: "uuid-abc",
                email: "ana@teste.com",
                nomeCompleto: "Ana Conceição",
                roles: ["Gerente"],
                temSenha: false,
                googleVinculado: true,
            },
            token,
        );

        expect(usuario.id).toBe(7);
        expect(usuario.nome).toBe("Ana Conceição");
        expect(usuario.papeis).toEqual(["gerente"]);
        expect(usuario.administrativo).toBe(true);
        expect(usuario.isAdmin).toBe(true);
        expect(usuario.temSenha).toBe(false);
        expect(usuario.googleVinculado).toBe(true);
    });

    it("funciona so com o token, antes de o perfil chegar", () => {
        const usuario = montarUsuario(null, token);

        expect(usuario.uuid).toBe("uuid-abc");
        expect(usuario.papeis).toEqual(["cliente"]);
        expect(usuario.isAdmin).toBe(false);
        // Sem DTO, o padrao seguro e assumir que ha senha (esconde menos coisa).
        expect(usuario.temSenha).toBe(true);
    });

    it("cai para os papeis do token quando o perfil vem sem roles", () => {
        const admin = montarJwt({ sub: "uuid-adm", role: ["admin"] });
        const usuario = montarUsuario({ uuid: "uuid-adm", roles: [] }, admin);

        expect(usuario.papeis).toEqual(["admin"]);
        expect(usuario.isAdmin).toBe(true);
    });

    it("devolve null quando nao ha nem perfil nem token", () => {
        expect(montarUsuario(null, null)).toBeNull();
    });
});
