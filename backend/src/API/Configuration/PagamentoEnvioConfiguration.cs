using Glorific.Api.Common;
using Glorific.Api.Workers;
using Glorific.Application.Ports;
using Glorific.Infrastructure.Integrations.InfinitePay;

namespace Glorific.Api.Configuration;

/// <summary>
/// Composicao da vertical de checkout, pagamento e envio.
///
/// Fica num arquivo proprio, e nao solto no Program.cs, por dois motivos concretos:
/// varias frentes de trabalho editam o Program em paralelo e uma lista de registros longa vira o
/// ponto unico de conflito de merge; e o adaptador do gateway precisa nascer amarrado ao
/// HttpClient certo, o que e uma decisao de composicao — nao de pipeline.
///
/// Os servicos de aplicacao (CheckoutService, PagamentoService, PedidoService, EnvioService) NAO
/// aparecem aqui: eles sao registrados por convencao pelo AddApplication.
/// </summary>
public static class PagamentoEnvioConfiguration
{
    public static IServiceCollection AddPagamentoEEnvio(this IServiceCollection services)
    {
        ArgumentNullException.ThrowIfNull(services);

        // Cliente TIPADO amarrado ao cliente NOMEADO ja configurado no Program (BaseAddress e
        // timeout). Registrar pelo nome, e nao com uma nova lambda de configuracao, evita a
        // duplicidade classica: dois lugares definindo BaseAddress e o segundo ganhando em
        // silencio quando alguem troca a URL so num deles.
        services.AddHttpClient<IPaymentGateway, InfinitePayGateway>(NomesHttpClient.InfinitePay);

        // Workers. Leia o cabecalho das classes antes de escalar a API para mais de uma replica:
        // os dois sao single-instance por design.
        services.AddHostedService<EnvioProcessor>();

        // O de pagamento nao e opcional: sem ele a fila de eventos nunca drena (pagamento
        // aprovado durante indisponibilidade do gateway fica preso em AguardandoPagamento) e a
        // reserva de estoque do pix abandonado nunca volta para a prateleira.
        services.AddHostedService<PagamentoProcessor>();

        return services;
    }
}
