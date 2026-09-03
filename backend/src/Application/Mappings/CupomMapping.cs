using Glorific.Application.DTO.Promocoes;
using Glorific.Domain.Entities.Promocoes;
using Mapster;

namespace Glorific.Application.Mappings;

/// <summary>
/// Mapeamentos do cupom.
///
/// A normalizacao do codigo (Trim + maiusculas) vive AQUI, e nao no servico, porque tanto a
/// criacao quanto a alteracao escrevem no mesmo campo e o indice unico do banco e case sensitive.
/// Deixar isso no servico significa duas chamadas para lembrar; no mapeamento, quem esquecer nao
/// tem como esquecer — o unico caminho de DTO para entidade passa por esta linha.
/// </summary>
public sealed class CupomMapping : IRegister
{
    public void Register(TypeAdapterConfig config)
    {
        config.NewConfig<CupomCreateDto, Cupom>()
            .Map(destino => destino.Codigo, origem => origem.Codigo.Trim().ToUpperInvariant());

        config.NewConfig<CupomUpdateDto, Cupom>()
            .Map(destino => destino.Codigo, origem => origem.Codigo.Trim().ToUpperInvariant());

        config.NewConfig<Cupom, CupomResponseDto>()
            // Comparacao entre dois campos do proprio registro: nao depende de relogio nem de
            // consulta, entao pode viajar pronta para a tela.
            .Map(
                destino => destino.Esgotado,
                origem => origem.UsoMaximoTotal != null && origem.UsosAtuais >= origem.UsoMaximoTotal);

        // CupomUso nao ganha mapeamento: a listagem de usos precisa de e-mail do cliente e numero
        // do pedido, que moram em outras tabelas. Ela e projetada direto na consulta do servico,
        // no banco, em vez de materializar o ledger inteiro para mapear em memoria.
    }
}
