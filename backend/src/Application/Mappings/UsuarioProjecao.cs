using System.Linq.Expressions;
using Glorific.Application.Common;
using Glorific.Application.DTO.Identidade;
using Glorific.Domain.Entities.Identidade;

namespace Glorific.Application.Mappings;

/// <summary>
/// A UNICA forma de transformar Usuario em UsuarioResponseDto.
///
/// Por que uma Expression e nao um IRegister do Mapster, como o resto do projeto: os tres
/// campos que interessam aqui — Roles, TemSenha e GoogleVinculado — moram em OUTRAS tabelas ou
/// derivam de uma coluna que nunca pode sair do servidor. Com Mapster seria preciso carregar
/// usuarios_roles e logins_externos com Include para depois jogar quase tudo fora; como
/// Expression, o Postgres resolve os tres no mesmo SELECT e o hash de senha nem chega a
/// trafegar do banco para a aplicacao.
///
/// Escrever isto uma vez so tambem garante que /auth/me, o perfil da conta e a listagem
/// administrativa devolvam exatamente o mesmo formato — no repo de referencia cada endpoint
/// montava o seu, e "temSenha" existia em um e faltava no outro.
/// </summary>
public static class UsuarioProjecao
{
    public static readonly Expression<Func<Usuario, UsuarioResponseDto>> Resposta = usuario =>
        new UsuarioResponseDto
        {
            Id = usuario.Id,
            Uuid = usuario.Uuid,
            Email = usuario.Email,
            EmailVerificado = usuario.EmailVerificado,
            NomeCompleto = usuario.NomeCompleto,
            Cpf = usuario.Cpf,
            Telefone = usuario.Telefone,
            FotoUrl = usuario.FotoUrl,
            DataNascimento = usuario.DataNascimento,
            AceitaMarketing = usuario.AceitaMarketing,
            Ativo = usuario.Ativo,
            DataCriacao = usuario.DataCriacao,
            UltimoLoginEm = usuario.UltimoLoginEm,

            // Vira "senha_hash IS NOT NULL" no SQL: o hash responde a pergunta sem sair do banco.
            TemSenha = usuario.SenhaHash != null,

            GoogleVinculado = usuario.LoginsExternos.Any(
                login => login.Provedor == ProvedoresLoginExterno.Google),

            Roles = usuario.Roles.Select(vinculo => vinculo.Role.Nome).ToList()
        };
}
