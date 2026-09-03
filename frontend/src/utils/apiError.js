/**
 * Extrator canonico de erro da API.
 * Envelope oficial do backend: `{ statusCode, error, traceId }`.
 * `detail`/`title` cobrem ProblemDetails vindo de middleware/validacao.
 */
export function getApiError(err) {
    const data = err?.response?.data;
    return {
        status: err?.response?.status ?? null,
        message:
            data?.error ||
            data?.detail ||
            data?.title ||
            err?.message ||
            "Erro inesperado",
        traceId: data?.traceId ?? null,
        /** Erros de validacao por campo: `{ campo: ["msg"] }` (ModelState do .NET). */
        errors: data?.errors ?? null,
    };
}

/** Atalho para o caso "404 e estado normal" tratado dentro do service. */
export function ehNaoEncontrado(err) {
    return err?.response?.status === 404;
}

export default getApiError;
