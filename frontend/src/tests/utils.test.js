import { describe, expect, it } from "vitest";
import { formatarCentavosParaBRL, mascaraPrecoCentavos } from "@/utils/financeiro.js";
import { formatCPF, formatCEP, formatTelefone, isValidCPF } from "@/utils/masks.js";
import { montarPaginas } from "@/components/ui/Paginacao.jsx";

/** O Intl separa "R$" do numero com espaco nao separavel (U+00A0 ou U+202F). */
const normalizarEspacos = (texto) => texto.replace(/\s/g, " ");

describe("financeiro", () => {
    it("formata centavos em BRL", () => {
        expect(normalizarEspacos(formatarCentavosParaBRL(48900))).toBe("R$ 489,00");
        expect(normalizarEspacos(formatarCentavosParaBRL(0))).toBe("R$ 0,00");
    });

    it("preenche os centavos da direita para a esquerda", () => {
        expect(mascaraPrecoCentavos("1299")).toBe("12,99");
        expect(mascaraPrecoCentavos("5")).toBe("0,05");
    });
});

describe("masks", () => {
    it("aplica as mascaras de CPF, CEP e telefone", () => {
        expect(formatCPF("12345678909")).toBe("123.456.789-09");
        expect(formatCEP("81070001")).toBe("81070-001");
        expect(formatTelefone("41984485264")).toBe("(41) 98448-5264");
    });

    it("valida os digitos verificadores do CPF", () => {
        expect(isValidCPF("123.456.789-09")).toBe(true);
        expect(isValidCPF("111.111.111-11")).toBe(false);
    });
});

describe("paginacao", () => {
    it("insere elipse entre blocos distantes", () => {
        expect(montarPaginas(5, 10)).toEqual([1, "...", 4, 5, 6, "...", 10]);
    });
});
