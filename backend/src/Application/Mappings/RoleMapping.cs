using Glorific.Application.DTO.Identidade;
using Glorific.Domain.Constants;
using Glorific.Domain.Entities.Identidade;
using Mapster;

namespace Glorific.Application.Mappings;

/// <summary>
/// EXEMPLO DE REFERENCIA do padrao de mapeamento do projeto — copie a forma daqui.
///
/// Regras que este arquivo demonstra:
/// 1. Um arquivo por agregado, classe sealed, implementando IRegister. E o IRegister que faz o
///    Scan do MapsterConfig encontrar o mapeamento; classe estatica com metodo "Configure"
///    NAO e encontrada por scan nenhum, e foi assim que dezesseis mapeamentos ficaram
///    inativos no repo de referencia.
/// 2. Todo campo derivado e declarado explicitamente. Convencao de nome resolve o trivial;
///    o que depende de regra (aqui, "este papel abre o painel admin?") tem de estar escrito.
/// 3. Sentido do mapeamento sempre explicito: entidade -> ResponseDto para saida,
///    CreateDto/UpdateDto -> entidade para entrada.
/// </summary>
public sealed class RoleMapping : IRegister
{
    public void Register(TypeAdapterConfig config)
    {
        config.NewConfig<Role, RoleResponseDto>()
            .Map(destino => destino.Administrativo, origem => Roles.Administrativos.Contains(origem.Nome));
    }
}
