/**
 * Ponte service -> UI (pub/sub de um ouvinte so).
 *
 * Existe porque o interceptor do axios nao pode usar hooks: ele emite aqui e o
 * `ToastProvider` assina no mount. Sem isso, cada service teria que receber um
 * callback de toast por parametro.
 */
let listener = null;

export const toastBus = {
    /** O provider chama isso no mount. O ultimo a assinar vence (so ha um provider). */
    subscribe(fn) {
        listener = fn;
        return () => {
            if (listener === fn) listener = null;
        };
    },

    /** @param {string} message @param {'success'|'error'|'warning'|'info'} type */
    emit(message, type = "error") {
        if (listener) listener(message, type);
    },
};
