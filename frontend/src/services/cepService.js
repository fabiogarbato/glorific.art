/**
 * Busca de endereco por CEP (ViaCEP).
 *
 * POR QUE NAO PASSA PELO `api/client.js`: o backend nao expoe rota de CEP, entao
 * a consulta e feita direto no ViaCEP. O client unico do projeto injeta o Bearer
 * do usuario em toda requisicao — mandar o nosso JWT para um host de terceiro
 * seria vazar credencial. Por isso aqui usa-se o axios "cru" (`axios.get`, sem
 * `axios.create` e sem interceptor), que nao carrega token nenhum.
 *
 * A regra que continua valendo: a PAGINA nao importa axios. Ela chama um hook,
 * que chama este service.
 *
 * O ViaCEP nunca devolve 404: CEP inexistente vem 200 com `{ erro: true }`.
 */
import axios from "axios";
import { onlyDigits } from "@/utils/masks.js";

const TIMEOUT_MS = 8000;

export const cepService = {
    /**
     * @returns {Promise<null | { cep, logradouro, bairro, cidade, uf }>}
     * `null` quando o CEP nao existe ou o servico nao respondeu — quem chama
     * mantem os campos editaveis em vez de travar o cadastro.
     */
    async buscar(cep) {
        const digitos = onlyDigits(cep);
        if (digitos.length !== 8) return null;

        try {
            const { data } = await axios.get(`https://viacep.com.br/ws/${digitos}/json/`, {
                timeout: TIMEOUT_MS,
            });

            if (!data || data.erro) return null;

            return {
                cep: digitos,
                logradouro: data.logradouro ?? "",
                // O ViaCEP chama de "bairro"; capitais grandes as vezes devolvem
                // vazio, e por isso o campo continua editavel na tela.
                bairro: data.bairro ?? "",
                cidade: data.localidade ?? "",
                uf: (data.uf ?? "").toUpperCase(),
            };
        } catch {
            // Servico de terceiro fora do ar nao pode derrubar o checkout: a
            // pessoa preenche na mao.
            return null;
        }
    },
};

export default cepService;
