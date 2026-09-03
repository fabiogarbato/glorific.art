using Glorific.Application.Common;
using Glorific.Domain.Constants;
using Glorific.Domain.Entities.Identidade;
using Glorific.Domain.Interfaces;
using Glorific.Infrastructure.Data;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;

namespace Glorific.Infrastructure.Seeding;

/// <summary>
/// Cria o administrador inicial a partir de variaveis de ambiente. Idempotente.
///
/// REGRA QUE NAO SE NEGOCIA: nao existe senha padrao no codigo. Um "admin/admin123" de fabrica
/// sobrevive ao deploy, aparece em varredura automatizada no primeiro dia e entrega o painel
/// inteiro. Sem ADMIN_EMAIL e ADMIN_SENHA definidos, este seeder nao faz NADA e registra um
/// aviso — a loja sobe sem admin, o que e um problema visivel e corrigivel, ao contrario de uma
/// credencial conhecida.
///
/// Idempotencia: se o e-mail ja existe, a senha NAO e sobrescrita (senao um restart do container
/// desfaria a troca de senha que o admin fez pelo painel) — apenas o papel admin e garantido.
/// </summary>
public static class SeedAdmin
{
    public const string VariavelEmail = "ADMIN_EMAIL";

    /// <summary>Nome principal da variavel de senha.</summary>
    public const string VariavelSenha = "ADMIN_SENHA";

    /// <summary>Nome alternativo aceito, para nao quebrar deploy que ja usa o outro.</summary>
    public const string VariavelSenhaAlternativa = "ADMIN_SENHA_INICIAL";

    private const int TamanhoMinimoSenha = 12;

    public static async Task ExecutarAsync(
        GlorificContext contexto,
        IConfiguration configuration,
        ILogger logger,
        IClock relogio,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(contexto);
        ArgumentNullException.ThrowIfNull(configuration);
        ArgumentNullException.ThrowIfNull(logger);
        ArgumentNullException.ThrowIfNull(relogio);

        var email = (configuration[VariavelEmail] ?? string.Empty).Trim().ToLowerInvariant();

        var senha = (configuration[VariavelSenha] ?? configuration[VariavelSenhaAlternativa] ?? string.Empty).Trim();

        if (email.Length == 0 || senha.Length == 0)
        {
            logger.LogWarning(
                "Seed do admin: {Email}/{Senha} nao definidos. Nenhum administrador foi criado. " +
                "Defina as duas variaveis e reinicie para provisionar o acesso ao painel.",
                VariavelEmail,
                VariavelSenha);
            return;
        }

        if (!email.Contains('@', StringComparison.Ordinal))
        {
            logger.LogWarning("Seed do admin: {Email} nao parece um e-mail valido. Nada foi criado.", VariavelEmail);
            return;
        }

        // Exigencia maior que a do cadastro publico: esta e a conta que abre a loja inteira.
        if (senha.Length < TamanhoMinimoSenha)
        {
            logger.LogWarning(
                "Seed do admin: a senha informada tem menos de {Minimo} caracteres. Nada foi criado.",
                TamanhoMinimoSenha);
            return;
        }

        var papelAdmin = await contexto.Roles
            .FirstOrDefaultAsync(role => role.Nome == Roles.Admin, cancellationToken);

        if (papelAdmin is null)
        {
            logger.LogWarning(
                "Seed do admin: o papel '{Papel}' nao existe. O seed de referencia precisa rodar antes.",
                Roles.Admin);
            return;
        }

        var agora = relogio.UtcNow;

        var usuario = await contexto.Usuarios
            .FirstOrDefaultAsync(u => u.Email == email, cancellationToken);

        if (usuario is null)
        {
            usuario = new Usuario
            {
                Uuid = Guid.NewGuid().ToString(),
                Email = email,
                EmailVerificado = true,
                NomeCompleto = "Administrador",
                SenhaHash = Senhas.Hash(senha),
                Ativo = true
            };

            await contexto.Usuarios.AddAsync(usuario, cancellationToken);

            // SaveChanges aqui porque o vinculo de papel precisa do Id gerado. O chamador roda
            // isto no boot, fora de qualquer requisicao, entao nao ha transacao a compor.
            await contexto.SaveChangesAsync(cancellationToken);

            logger.LogWarning(
                "Seed do admin: usuario administrador criado para {Email}. " +
                "Troque a senha pelo painel no primeiro acesso.",
                email);
        }
        else
        {
            // Conta existente NAO tem a senha sobrescrita: um restart do container desfaria a
            // troca de senha feita pelo painel e devolveria o acesso a quem tem a env antiga.
            logger.LogInformation(
                "Seed do admin: ja existe conta para {Email}. Senha preservada; apenas o papel e garantido.",
                email);

            if (!usuario.Ativo)
            {
                // Ficar sem nenhum admin ativo tranca a loja por fora.
                usuario.Ativo = true;
                logger.LogWarning("Seed do admin: a conta {Email} estava desativada e foi reativada.", email);
            }
        }

        var jaTemPapel = await contexto.UsuariosRoles
            .AnyAsync(vinculo => vinculo.IdUsuario == usuario.Id && vinculo.IdRole == papelAdmin.Id, cancellationToken);

        if (!jaTemPapel)
        {
            await contexto.UsuariosRoles.AddAsync(
                new UsuarioRole
                {
                    IdUsuario = usuario.Id,
                    IdRole = papelAdmin.Id,
                    ConcedidaEm = agora,

                    // Null de proposito: veio do provisionamento, nao de uma promocao humana.
                    ConcedidaPor = null
                },
                cancellationToken);
        }

        await contexto.SaveChangesAsync(cancellationToken);
    }
}
