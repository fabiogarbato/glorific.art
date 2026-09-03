import { afterEach, beforeEach, describe, expect, it, vi } from "vitest";
import api, { registrarTratadorDeNaoAutorizado } from "@/api/client.js";
import { FOLGA_RENOVACAO_MS, msAteRenovar } from "@/contexts/AuthContext.jsx";

/**
 * Decisao de renovacao.
 *
 * Duas coisas separadas, e ambas ja causaram sessao caindo em producao noutros
 * projetos: QUANDO agendar o refresh, e o que fazer quando o 401 chega mesmo
 * assim. A segunda parte e onde mora o risco de laco infinito.
 */

const AGORA = new Date("2026-09-03T12:00:00Z").getTime();

/** `exp` do JWT em segundos, daqui a N segundos. */
const expDaquiA = (segundos) => Math.floor(AGORA / 1000) + segundos;

describe("quando renovar o access token", () => {
    it("agenda a folga antes do vencimento", () => {
        // Token curto o bastante para caber no teto de 10 min: aqui quem
        // manda e a folga.
        const atraso = msAteRenovar(expDaquiA(5 * 60), AGORA);

        expect(atraso).toBe(5 * 60_000 - FOLGA_RENOVACAO_MS);
    });

    it("no token de 15 minutos da producao, quem manda e o teto", () => {
        // "AccessTokenMinutos: 15" no appsettings.json. A folga daria 14
        // min, mas o teto de 10 min vem antes — e renovar cedo demais nao
        // custa nada perto de confiar num setTimeout de quatorze minutos.
        expect(msAteRenovar(expDaquiA(15 * 60), AGORA)).toBe(10 * 60_000);
    });

    it("renova na hora quando o token ja venceu", () => {
        expect(msAteRenovar(expDaquiA(-10), AGORA)).toBe(0);
    });

    it("renova na hora quando o vencimento cai dentro da folga", () => {
        // 30 s de vida com folga de 60 s: esperar seria entregar um token morto.
        expect(msAteRenovar(expDaquiA(30), AGORA)).toBe(0);
        expect(msAteRenovar(expDaquiA(60), AGORA)).toBe(0);
    });

    it("nao agenda quando nao ha exp utilizavel", () => {
        expect(msAteRenovar(undefined, AGORA)).toBeNull();
        expect(msAteRenovar(null, AGORA)).toBeNull();
        expect(msAteRenovar(0, AGORA)).toBeNull();
        expect(msAteRenovar("amanha", AGORA)).toBeNull();
    });

    it("limita o agendamento para nao confiar em setTimeout de horas", () => {
        const atraso = msAteRenovar(expDaquiA(24 * 60 * 60), AGORA);

        expect(atraso).toBe(10 * 60_000);
    });

    it("nunca devolve atraso negativo", () => {
        for (const segundos of [-3600, -1, 0, 1, 59, 61, 900]) {
            const atraso = msAteRenovar(expDaquiA(segundos), AGORA);
            if (atraso !== null) expect(atraso).toBeGreaterThanOrEqual(0);
        }
    });
});

describe("401 vai para o dono da sessao", () => {
    const adapterOriginal = api.defaults.adapter;
    let cancelar = null;

    beforeEach(() => {
        cancelar = null;
    });

    afterEach(() => {
        api.defaults.adapter = adapterOriginal;
        if (cancelar) cancelar();
    });

    /** Adapter que responde 401 nas N primeiras chamadas e 200 depois. */
    function adapterQueFalha(vezes) {
        let chamadas = 0;
        return vi.fn(async (config) => {
            chamadas += 1;
            if (chamadas <= vezes) {
                const erro = new Error("Nao autorizado");
                erro.config = config;
                erro.response = { status: 401, data: {}, config };
                throw erro;
            }
            return { status: 200, statusText: "OK", data: { ok: true }, headers: {}, config };
        });
    }

    it("deixa o tratador renovar e repetir o request original", async () => {
        const adapter = adapterQueFalha(1);
        api.defaults.adapter = adapter;

        const renovacoes = vi.fn();
        cancelar = registrarTratadorDeNaoAutorizado(async (erro) => {
            renovacoes();
            erro.config.__jaRenovou = true;
            return api.request(erro.config);
        });

        const { data } = await api.get("/pedidos");

        expect(data).toEqual({ ok: true });
        expect(renovacoes).toHaveBeenCalledTimes(1);
        expect(adapter).toHaveBeenCalledTimes(2);
    });

    it("nao entra em laco quando a renovacao tambem falha", async () => {
        const adapter = adapterQueFalha(Number.POSITIVE_INFINITY);
        api.defaults.adapter = adapter;

        const renovacoes = vi.fn();
        cancelar = registrarTratadorDeNaoAutorizado(async (erro) => {
            // Mesma regra do AuthProvider: uma tentativa por request, e nenhuma
            // para as rotas de entrada (que chegam com __semRenovar).
            if (erro.config.__jaRenovou || erro.config.__semRenovar) throw erro;
            renovacoes();
            erro.config.__jaRenovou = true;
            return api.request(erro.config);
        });

        await expect(api.get("/pedidos")).rejects.toMatchObject({
            response: { status: 401 },
        });

        expect(renovacoes).toHaveBeenCalledTimes(1);
        expect(adapter).toHaveBeenCalledTimes(2);
    });

    it("nao tenta renovar a partir das rotas de entrada", async () => {
        const adapter = adapterQueFalha(Number.POSITIVE_INFINITY);
        api.defaults.adapter = adapter;

        const renovacoes = vi.fn();
        cancelar = registrarTratadorDeNaoAutorizado(async (erro) => {
            if (erro.config.__jaRenovou || erro.config.__semRenovar) throw erro;
            renovacoes();
            erro.config.__jaRenovou = true;
            return api.request(erro.config);
        });

        await expect(
            api.post("/auth/login", { email: "a@b.com" }, { __semRenovar: true }),
        ).rejects.toMatchObject({ response: { status: 401 } });

        expect(renovacoes).not.toHaveBeenCalled();
        expect(adapter).toHaveBeenCalledTimes(1);
    });
});
