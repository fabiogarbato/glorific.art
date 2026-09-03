using Glorific.Domain.Interfaces;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Design;

namespace Glorific.Infrastructure.Data;

/// <summary>
/// Fabrica de design-time — e por aqui que `dotnet ef migrations add` monta o contexto.
///
/// CRITICO: a primeira linha executavel e o switch do Npgsql, espelhando o Program.cs.
/// O `dotnet ef` NUNCA roda o Program.cs; se o switch nao estiver aqui tambem, o design-time
/// mapeia timestamptz enquanto o runtime mapeia timestamp, a migration nasce divergente do
/// modelo e o PendingModelChangesWarning derruba a API no boot (EF 9+ lanca excecao).
/// Foi exatamente esse o postmortem do repo de referencia.
/// </summary>
public sealed class GlorificContextFactory : IDesignTimeDbContextFactory<GlorificContext>
{
    private const string ConexaoFallback =
        "Host=localhost;Port=5433;Database=glorific;Username=glorific;Password=glorific";

    public GlorificContext CreateDbContext(string[] args)
    {
        AppContext.SetSwitch("Npgsql.EnableLegacyTimestampBehavior", true);

        var conexao = Environment.GetEnvironmentVariable("ConnectionStrings__DefaultConnection");
        if (string.IsNullOrWhiteSpace(conexao))
            conexao = ConexaoFallback;

        var opcoes = new DbContextOptionsBuilder<GlorificContext>()
            .UseNpgsql(conexao, npg => npg.MigrationsAssembly(typeof(GlorificContext).Assembly.GetName().Name))
            .Options;

        // Design-time nao tem container de DI: o relogio e fixo e nunca chega a ser usado,
        // porque scaffolding de migration nao materializa entidade nem chama SaveChanges.
        return new GlorificContext(opcoes, new RelogioDesignTime());
    }

    private sealed class RelogioDesignTime : IClock
    {
        public DateTime UtcNow { get; } = new DateTime(2026, 1, 1, 0, 0, 0, DateTimeKind.Utc);
    }
}
