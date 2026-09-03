using Glorific.Application.DTO.Painel;

namespace Glorific.Application.Services.Interfaces;

/// <summary>
/// Metricas da tela inicial do painel.
///
/// Regra dura desta area: TODA metrica e agregada no banco. Nenhuma tabela e materializada em
/// memoria para ser somada em C#. E a diferenca entre um painel que abre em 200 ms no primeiro ano
/// e um painel que derruba a API quando a loja passa de dez mil pedidos.
///
/// Aqui nao ha paginacao: as listas sao rankings e filas com limite fixo e pequeno, definido pelo
/// servico e nao pelo chamador.
/// </summary>
public interface IDashboardService
{
    /// <summary>
    /// Resumo completo do painel. Sem datas informadas, usa os ultimos 30 dias.
    /// As datas sao interpretadas em UTC, que e como tudo e gravado.
    /// </summary>
    Task<DashboardResumoDto> ObterResumoAsync(
        DateTime? de = null,
        DateTime? ate = null,
        CancellationToken cancellationToken = default);
}
