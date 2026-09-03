using System.Globalization;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace Glorific.Infrastructure.Integrations.MelhorEnvio;

/// <summary>
/// Serializacao da fronteira com o microservico integracaoMelhorEnvio.
///
/// A armadilha numero 1 desta integracao esta aqui: o microservico recebe camelCase e devolve
/// snake_case. Ele nao configura AddJsonOptions, entao vale o default web do ASP.NET na
/// ENTRADA (camelCase, case-insensitive — "postal_code" NAO liga, porque case-insensitive nao
/// remove underscore); e quase toda SAIDA e o corpo cru do Melhor Envio repassado byte a byte,
/// que e snake_case. Sao dois JsonSerializerOptions diferentes de proposito.
///
/// A excecao e /api/auth/status: unico contrato TIPADO do microservico, e portanto camelCase
/// tambem na saida. Usar as opcoes erradas ali devolve um objeto com todos os campos zerados,
/// sem erro nenhum — o pior tipo de bug de integracao.
/// </summary>
internal static class MelhorEnvioJson
{
    /// <summary>
    /// Corpo ENVIADO ao microservico: camelCase.
    /// WhenWritingNull nao e cosmetico — o contrato exige que campos como nonCommercial e
    /// invoice sumam do payload quando nao se aplicam, e nao que viajem como null.
    /// </summary>
    public static readonly JsonSerializerOptions Envio = new(JsonSerializerDefaults.Web)
    {
        DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull
    };

    /// <summary>Resposta TIPADA do microservico (/api/auth/status): camelCase.</summary>
    public static readonly JsonSerializerOptions RespostaMicroservico = new(JsonSerializerDefaults.Web)
    {
        NumberHandling = JsonNumberHandling.AllowReadingFromString
    };

    // A resposta REPASSADA do Melhor Envio (snake_case) NAO tem JsonSerializerOptions proprio:
    // ela e lida campo a campo com os helpers abaixo, sobre JsonDocument. O motivo e o
    // passthrough cru — o mesmo campo alterna entre string e numero ("price"), "error" ja
    // chegou string, objeto e lista, e um unico campo fora do tipo esperado derrubaria a
    // desserializacao da cotacao INTEIRA em vez de invalidar so a linha daquele servico.

    /// <summary>
    /// Le uma propriedade de objeto sem explodir quando ela nao existe ou veio null.
    /// A resposta do parceiro e passthrough cru: campo ausente e o caso comum, nao a excecao.
    /// </summary>
    public static bool TentarObter(JsonElement objeto, string propriedade, out JsonElement valor)
    {
        valor = default;

        if (objeto.ValueKind != JsonValueKind.Object)
            return false;

        if (!objeto.TryGetProperty(propriedade, out var encontrado))
            return false;

        if (encontrado.ValueKind is JsonValueKind.Null or JsonValueKind.Undefined)
            return false;

        valor = encontrado;
        return true;
    }

    /// <summary>Texto de um campo que pode chegar como string, numero ou booleano.</summary>
    public static string? Texto(JsonElement objeto, string propriedade)
    {
        if (!TentarObter(objeto, propriedade, out var valor))
            return null;

        var texto = valor.ValueKind switch
        {
            JsonValueKind.String => valor.GetString(),
            JsonValueKind.Number => valor.ToString(),
            JsonValueKind.True => "true",
            JsonValueKind.False => "false",
            _ => valor.ToString()
        };

        return string.IsNullOrWhiteSpace(texto) ? null : texto;
    }

    /// <summary>Decimal tolerante: o parceiro alterna entre "24.90" (string) e 24.90 (numero).</summary>
    public static decimal? Decimal(JsonElement objeto, string propriedade)
    {
        if (!TentarObter(objeto, propriedade, out var valor))
            return null;

        if (valor.ValueKind == JsonValueKind.Number && valor.TryGetDecimal(out var numero))
            return numero;

        if (valor.ValueKind == JsonValueKind.String
            && decimal.TryParse(valor.GetString(), NumberStyles.Any, CultureInfo.InvariantCulture, out var texto))
            return texto;

        return null;
    }

    public static int? Inteiro(JsonElement objeto, string propriedade)
    {
        var decimalValor = Decimal(objeto, propriedade);
        return decimalValor is null ? null : (int)Math.Round(decimalValor.Value, MidpointRounding.AwayFromZero);
    }

    public static bool? Booleano(JsonElement objeto, string propriedade)
    {
        if (!TentarObter(objeto, propriedade, out var valor))
            return null;

        return valor.ValueKind switch
        {
            JsonValueKind.True => true,
            JsonValueKind.False => false,
            JsonValueKind.Number => valor.TryGetInt32(out var numero) && numero != 0,
            JsonValueKind.String => bool.TryParse(valor.GetString(), out var texto) ? texto : null,
            _ => null
        };
    }

    /// <summary>
    /// Data do parceiro para DateTime em UTC.
    /// O ME devolve "2026-09-03 14:05:00" (sem fuso) e ISO com offset, dependendo do campo.
    /// Normalizar para UTC aqui e o que impede o horario de rastreio chegar tres horas errado
    /// na timeline do cliente.
    /// </summary>
    public static DateTime? DataUtc(JsonElement objeto, string propriedade)
    {
        var texto = Texto(objeto, propriedade);

        if (string.IsNullOrWhiteSpace(texto))
            return null;

        if (DateTimeOffset.TryParse(texto, CultureInfo.InvariantCulture, DateTimeStyles.AssumeUniversal, out var comFuso))
            return comFuso.UtcDateTime;

        if (DateTime.TryParse(texto, CultureInfo.InvariantCulture, DateTimeStyles.AdjustToUniversal, out var semFuso))
            return DateTime.SpecifyKind(semFuso, DateTimeKind.Utc);

        return null;
    }

    /// <summary>
    /// Mensagem de erro de um item de cotacao. O campo "error" do ME e string na maioria das
    /// vezes, mas ja chegou objeto e lista — desserializar como string quebraria a cotacao
    /// INTEIRA por causa de um unico servico indisponivel.
    /// </summary>
    public static string? MensagemErro(JsonElement objeto)
    {
        if (!TentarObter(objeto, "error", out var erro))
            return null;

        return erro.ValueKind switch
        {
            JsonValueKind.String => erro.GetString(),
            JsonValueKind.Object or JsonValueKind.Array => erro.ToString(),
            _ => erro.ToString()
        };
    }

    /// <summary>
    /// Normaliza a resposta de /calculate em lista.
    /// O parceiro devolve ARRAY na cotacao com varios servicos e OBJETO UNICO quando "services"
    /// tem um id so — que e exatamente o caso da recotacao do checkout.
    /// </summary>
    public static IReadOnlyList<JsonElement> ComoLista(JsonElement raiz)
    {
        if (raiz.ValueKind == JsonValueKind.Array)
            return [.. raiz.EnumerateArray()];

        if (raiz.ValueKind == JsonValueKind.Object)
            return [raiz];

        return [];
    }
}
