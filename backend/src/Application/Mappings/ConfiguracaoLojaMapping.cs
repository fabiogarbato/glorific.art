using Glorific.Application.DTO.Config;
using Glorific.Domain.Entities.Config;
using Mapster;

namespace Glorific.Application.Mappings;

/// <summary>
/// Configuracao da loja: colunas tipadas de um lado, record imutavel do outro. A convencao de
/// nome resolve todos os campos, e os dois mapeamentos ficam declarados mesmo assim para que o
/// Compile do boot valide os dois sentidos — e nao so o que acontecer de ser exercitado primeiro.
///
/// O sentido UpdateDto -> entidade nao inclui CepOrigem normalizado: a normalizacao para digitos
/// depende do CepHelper e acontece no servico, junto da validacao que rejeita CEP invalido.
/// </summary>
public sealed class ConfiguracaoLojaMapping : IRegister
{
    public void Register(TypeAdapterConfig config)
    {
        config.NewConfig<ConfiguracaoLoja, ConfiguracaoLojaResponseDto>();

        config.NewConfig<ConfiguracaoLojaUpdateDto, ConfiguracaoLoja>()
            .Ignore(destino => destino.Id)
            .Ignore(destino => destino.DataCriacao)
            .Ignore(destino => destino.DataAlteracao!);
    }
}
