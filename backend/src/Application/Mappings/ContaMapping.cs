using Glorific.Application.DTO.Conta;
using Glorific.Domain.Entities.Clientes;
using Mapster;

namespace Glorific.Application.Mappings;

/// <summary>
/// Mapeamento da area de conta do cliente. Segue o padrao do RoleMapping: classe sealed,
/// IRegister, um arquivo por area — e o IRegister que o Scan do MapsterConfig encontra.
///
/// So a SAIDA e mapeada. A entrada (EnderecoCreateDto/EnderecoUpdateDto -> Endereco) fica no
/// servico, escrita a mao, porque cada campo passa por normalizacao que Mapster nao faz: CEP
/// vira so digitos, UF vai para maiuscula, telefone e documento perdem a mascara. Deixar o
/// Mapster copiar "12.345-678" direto para uma coluna de 8 caracteres estoura no banco, e o
/// erro sai como 500 de driver em vez de 400 com o campo culpado.
/// </summary>
public sealed class ContaMapping : IRegister
{
    public void Register(TypeAdapterConfig config)
    {
        config.NewConfig<Endereco, EnderecoResponseDto>();
    }
}
