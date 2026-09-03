using System.Reflection;
using Glorific.Application.Mappings;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;

namespace Glorific.Application;

/// <summary>
/// Composicao da camada de aplicacao.
///
/// DECISAO CENTRAL: o registro de servicos e POR CONVENCAO, nao por lista.
/// Cada par I&lt;Nome&gt;Service -> &lt;Nome&gt;Service encontrado neste assembly vira Scoped
/// automaticamente. O motivo e concreto: varias frentes de trabalho adicionam servicos em
/// paralelo, e uma lista manual transforma este arquivo no unico ponto de conflito de merge do
/// repositorio. Quem cria um servico novo nao precisa (nem deve) editar este arquivo.
///
/// O preco da convencao e o silencio: um servico com nome fora do padrao simplesmente nao e
/// registrado e o erro so aparece na resolucao do controller. Por isso o padrao de nome e regra
/// dura — sufixo "Service" na classe, mesma interface com "I" na frente.
/// </summary>
public static class DependencyInjection
{
    private const string SufixoServico = "Service";

    public static IServiceCollection AddApplication(this IServiceCollection services)
    {
        ArgumentNullException.ThrowIfNull(services);

        var assembly = typeof(DependencyInjection).Assembly;

        services.AddServicosPorConvencao(assembly);

        // Scan + Compile. Mapeamento quebrado derruba o boot aqui, e nao na requisicao do cliente.
        MapsterConfig.Registrar(assembly);

        return services;
    }

    /// <summary>
    /// Varre o assembly e registra como Scoped todo par I&lt;Nome&gt;Service -> &lt;Nome&gt;Service.
    ///
    /// Scoped e nao Singleton porque o servico depende de repositorio, que depende do DbContext
    /// da requisicao. Singleton aqui vazaria o ChangeTracker de um cliente para outro.
    /// </summary>
    private static IServiceCollection AddServicosPorConvencao(this IServiceCollection services, Assembly assembly)
    {
        foreach (var implementacao in TiposConcretos(assembly))
        {
            var nomeContrato = "I" + implementacao.Name;

            var contrato = implementacao
                .GetInterfaces()
                .FirstOrDefault(i => string.Equals(i.Name, nomeContrato, StringComparison.Ordinal));

            if (contrato is null)
                continue;

            // TryAdd: um registro explicito feito antes (decorator, implementacao alternativa
            // por ambiente) continua valendo. A convencao preenche a lacuna, nao sobrescreve.
            services.TryAddScoped(contrato, implementacao);
        }

        return services;
    }

    private static IEnumerable<Type> TiposConcretos(Assembly assembly) =>
        assembly
            .GetTypes()
            .Where(tipo =>
                tipo is { IsClass: true, IsAbstract: false, IsGenericTypeDefinition: false }
                && tipo.IsPublic
                // GenericService<,,,> e base compartilhada, nao um servico registravel.
                && tipo.Name.EndsWith(SufixoServico, StringComparison.Ordinal));
}
