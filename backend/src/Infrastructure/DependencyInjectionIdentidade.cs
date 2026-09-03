using Glorific.Application.Ports;
using Glorific.Infrastructure.Auth;
using Glorific.Infrastructure.Email;
using Glorific.Infrastructure.Security;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;

namespace Glorific.Infrastructure;

/// <summary>
/// Adaptadores da vertical de identidade: emissao de token, validacao do id_token do Google e
/// token de redefinicao de senha.
///
/// Arquivo separado do <see cref="DependencyInjection"/> de proposito. Varias frentes de
/// trabalho registram adaptadores em paralelo, e concentrar tudo num arquivo so transforma ele
/// no unico ponto de conflito de merge do repositorio.
///
/// Tudo aqui e SINGLETON: nenhuma destas classes tem estado por requisicao e nenhuma depende do
/// DbContext. Registrar como Scoped so pagaria a construcao de novo a cada chamada — e, no caso
/// do TokenService, refazendo a validacao da chave e o SigningCredentials em todo login.
///
/// TryAdd em todos: um registro explicito feito antes (adaptador real de e-mail, dublê de
/// teste) continua valendo. A convencao preenche a lacuna, nao sobrescreve.
/// </summary>
public static class DependencyInjectionIdentidade
{
    public static IServiceCollection AddIdentidadeInfrastructure(this IServiceCollection services)
    {
        ArgumentNullException.ThrowIfNull(services);

        services.TryAddSingleton<ITokenService, TokenService>();
        services.TryAddSingleton<ITokenRedefinicaoSenha, TokenRedefinicaoSenhaHmac>();
        services.TryAddSingleton<IGoogleTokenValidator, GoogleTokenValidator>();

        // Borda: sem nenhum IEmailSender registrado, o AuthController inteiro deixaria de
        // resolver por causa do unico endpoint que envia e-mail. Este substituto so registra um
        // aviso no log e e trocado assim que o adaptador SMTP de verdade for registrado.
        services.TryAddSingleton<IEmailSender, EmailSenderLog>();

        return services;
    }
}
