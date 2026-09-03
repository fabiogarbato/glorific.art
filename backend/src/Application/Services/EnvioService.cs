using Glorific.Application.Models.MelhorEnvio;
using Glorific.Application.Ports;
using Glorific.Application.Ports.Options;
using Glorific.Application.Services.Interfaces;
using Glorific.Domain.Entities.Pedidos;
using Glorific.Domain.Enums;
using Glorific.Domain.Helpers;
using Glorific.Domain.Interfaces;
using Glorific.Domain.Interfaces.Repositories;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace Glorific.Application.Services;

/// <summary>
/// Maquina de estados da contratacao de etiqueta no Melhor Envio.
///
/// Pendente -> NoCarrinho -> Comprado -> EtiquetaGerada, PERSISTINDO DEPOIS DE CADA PASSO. Isso
/// nao e zelo: o passo de compra debita a carteira do lojista, e uma queda entre "comprei" e
/// "gravei" faria a proxima execucao comprar de novo. Gravando o MeOrderId assim que ele existe,
/// a retomada continua de onde parou.
///
/// Nada aqui lanca para o chamador do worker. Falha de parceiro vira UltimoErro mais backoff;
/// dado corrompido (pedido sem endereco ou sem itens) vira Falha DIRETA sem retry, porque
/// insistir oito vezes num pedido quebrado so atrasa os que dao certo.
/// </summary>
public sealed class EnvioService : IEnvioService
{
    /// <summary>A coluna de erro e limitada: stack trace do parceiro nao pode estourar a linha.</summary>
    private const int LimiteUltimoErro = 2000;

    private readonly IEnvioRepository _envios;
    private readonly IPedidoRepository _pedidos;
    private readonly IMelhorEnvioClient _melhorEnvio;
    private readonly IEmailSender _email;
    private readonly IUnitOfWork _unitOfWork;
    private readonly IClock _relogio;
    private readonly FreteOptions _frete;
    private readonly AppOptions _app;
    private readonly ILogger<EnvioService> _logger;

    public EnvioService(
        IEnvioRepository envios,
        IPedidoRepository pedidos,
        IMelhorEnvioClient melhorEnvio,
        IEmailSender email,
        IUnitOfWork unitOfWork,
        IClock relogio,
        IOptions<FreteOptions> frete,
        IOptions<AppOptions> app,
        ILogger<EnvioService> logger)
    {
        _envios = envios ?? throw new ArgumentNullException(nameof(envios));
        _pedidos = pedidos ?? throw new ArgumentNullException(nameof(pedidos));
        _melhorEnvio = melhorEnvio ?? throw new ArgumentNullException(nameof(melhorEnvio));
        _email = email ?? throw new ArgumentNullException(nameof(email));
        _unitOfWork = unitOfWork ?? throw new ArgumentNullException(nameof(unitOfWork));
        _relogio = relogio ?? throw new ArgumentNullException(nameof(relogio));
        _frete = frete?.Value ?? throw new ArgumentNullException(nameof(frete));
        _app = app?.Value ?? throw new ArgumentNullException(nameof(app));
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
    }

    /// <inheritdoc />
    public async Task EnfileirarAsync(int idPedido, CancellationToken cancellationToken = default)
    {
        // A unique em envios.id_pedido e a garantia real de uma etiqueta por pedido; esta
        // consulta so evita chegar ate o banco para tomar violacao no caminho feliz.
        if (await _envios.ObterPorPedidoAsync(idPedido, cancellationToken) is not null)
            return;

        var pedido = await _pedidos.ObterPorIdAsync(idPedido, cancellationToken);

        if (pedido is null || pedido.IdServicoFrete is null or <= 0)
        {
            _logger.LogError(
                "Pedido {Pedido} sem servico de frete: envio nao pode ser enfileirado.",
                idPedido);

            return;
        }

        var idServico = pedido.IdServicoFrete.Value;

        // Servico fora da whitelist de dispensa de nota nasce em AguardandoNota, e o worker nao o
        // pega ate o admin informar a chave. Em loja PJ de moda essa lista tende a ficar vazia,
        // o que faz de AguardandoNota o fluxo PADRAO — e da integracao fiscal um item de go-live.
        var status = _frete.ExigeNota(idServico) ? StatusEnvio.AguardandoNota : StatusEnvio.Pendente;

        var envio = new Envio
        {
            IdPedido = idPedido,
            IdServico = idServico,
            NomeServico = pedido.ServicoFrete,
            NomeTransportadora = pedido.TransportadoraFrete,
            // LIMITACAO CONHECIDA: o pedido guarda o frete COBRADO, e quando um cupom de frete
            // gratis ou a regra de frete gratis da loja zeram a cobranca, o valor cotado original
            // se perde. O custo real aparece em ValorCompradoCentavos depois da compra. Fechar
            // essa lacuna exige uma coluna de frete cotado em pedidos.
            ValorCotadoCentavos = pedido.FreteCentavos,
            Status = status,
            Tentativas = 0,
            DataCriacao = _relogio.UtcNow
        };

        await _envios.AdicionarAsync(envio, cancellationToken);
        await _unitOfWork.SaveChangesAsync(cancellationToken);

        _logger.LogInformation(
            "Envio enfileirado para o pedido {Numero} em {Status}.",
            pedido.Numero,
            status);
    }

    /// <inheritdoc />
    public async Task<int> ProcessarPendentesAsync(int limite, CancellationToken cancellationToken = default)
    {
        if (limite <= 0)
            return 0;

        var pendentes = await _envios.ObterPendentesAsync(_relogio.UtcNow, limite, cancellationToken);

        var processados = 0;

        foreach (var pendente in pendentes)
        {
            if (cancellationToken.IsCancellationRequested)
                break;

            try
            {
                if (await ProcessarAsync(pendente.Id, cancellationToken))
                    processados++;
            }
            catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
            {
                break;
            }
            catch (Exception excecao)
            {
                // Um envio quebrado nao pode levar o lote inteiro junto.
                _logger.LogError(excecao, "Falha nao tratada ao processar o envio {Envio}.", pendente.Id);
            }
        }

        return processados;
    }

    /// <inheritdoc />
    public async Task<bool> ProcessarAsync(int idEnvio, CancellationToken cancellationToken = default)
    {
        var agora = _relogio.UtcNow;

        // CLAIM ATOMICO ANTES DE QUALQUER I/O. Sem ele, o worker e o botao "gerar etiqueta" do
        // painel compram a mesma etiqueta ao mesmo tempo e a segunda e debitada sem ser usada.
        // O proprio claim ja incrementa Tentativas e empurra ProximaTentativaEm para o fim do lease.
        if (!await _envios.TentarReivindicarAsync(idEnvio, agora, cancellationToken))
            return false;

        // Reconsulta obrigatoria: o claim usa UPDATE direto e desanexa a linha do ChangeTracker.
        var envio = await _envios.ObterParaEdicaoAsync(idEnvio, cancellationToken);

        if (envio is null)
            return false;

        var pedido = await _pedidos.ObterCompletoAsync(envio.IdPedido, cancellationToken);

        if (pedido is null || pedido.EnderecoEntrega is null || pedido.Itens.Count == 0)
        {
            await MarcarFalhaDefinitivaAsync(
                envio, "Pedido sem endereco ou sem itens: dado inconsistente.", cancellationToken);

            return false;
        }

        try
        {
            var avancou = false;

            if (envio.Status == StatusEnvio.Pendente)
            {
                await InserirNoCarrinhoAsync(envio, pedido, cancellationToken);
                avancou = true;
            }

            if (envio.Status == StatusEnvio.NoCarrinho)
            {
                await ComprarAsync(envio, pedido, cancellationToken);
                avancou = true;
            }

            if (envio.Status == StatusEnvio.Comprado)
            {
                await GerarEtiquetaAsync(envio, pedido, cancellationToken);
                avancou = true;
            }

            if (envio.Status == StatusEnvio.EtiquetaGerada)
            {
                // Sai da fila do worker. ProximaTentativaEm em null evita que o lease vencido
                // reative um envio ja concluido.
                envio.ProximaTentativaEm = null;
                envio.UltimoErro = null;
                envio.DataAlteracao = _relogio.UtcNow;

                _envios.Atualizar(envio);
                await _unitOfWork.SaveChangesAsync(cancellationToken);
            }

            return avancou;
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
            throw;
        }
        catch (Exception excecao)
        {
            // A excecao concreta do parceiro mora na Infrastructure e esta camada nao a
            // referencia. Para o worker isso nao muda nada: qualquer falha vira backoff, e o
            // estado ja persistido diz de onde retomar.
            await RegistrarFalhaAsync(envio, excecao, cancellationToken);
            return false;
        }
    }

    /// <inheritdoc />
    public async Task<string?> ObterUrlEtiquetaAsync(
        int idPedido,
        bool publico = false,
        CancellationToken cancellationToken = default)
    {
        var existente = await _envios.ObterPorPedidoAsync(idPedido, cancellationToken);

        if (existente?.MeOrderId is null)
            return null;

        // Link privado ja conhecido serve; link publico e sempre gerado na hora, porque ele abre
        // para qualquer um com a URL e nao deve ficar guardado.
        if (!publico && !string.IsNullOrWhiteSpace(existente.UrlEtiqueta))
            return existente.UrlEtiqueta;

        var modo = publico ? ModoImpressaoEtiqueta.Publico : ModoImpressaoEtiqueta.Privado;

        var impressao = await _melhorEnvio.ImprimirEtiquetaAsync(
            [existente.MeOrderId], modo, cancellationToken);

        if (publico || string.IsNullOrWhiteSpace(impressao.Url))
            return impressao.Url;

        var envio = await _envios.ObterParaEdicaoAsync(existente.Id, cancellationToken);

        if (envio is not null)
        {
            envio.UrlEtiqueta = impressao.Url;
            envio.DataAlteracao = _relogio.UtcNow;

            _envios.Atualizar(envio);
            await _unitOfWork.SaveChangesAsync(cancellationToken);
        }

        return impressao.Url;
    }

    /// <inheritdoc />
    public async Task AtualizarRastreioAsync(int idEnvio, CancellationToken cancellationToken = default)
    {
        var envio = await _envios.ObterParaEdicaoAsync(idEnvio, cancellationToken);

        if (envio?.MeOrderId is null)
            return;

        var resultados = await _melhorEnvio.RastrearAsync([envio.MeOrderId], cancellationToken);
        var rastreio = resultados.FirstOrDefault(r => r.MeOrderId == envio.MeOrderId);

        if (rastreio is null)
            return;

        if (!string.IsNullOrWhiteSpace(rastreio.CodigoRastreio))
            envio.CodigoRastreio = rastreio.CodigoRastreio;

        var promovido = rastreio.StatusEquivalente;

        // O status local SO PROMOVE. O Melhor Envio reordena eventos e reenvia estados antigos;
        // deixar regredir faz um pedido entregue voltar para "postado" na tela do cliente.
        if ((promovido is StatusEnvio.Postado or StatusEnvio.Entregue)
            && (int)promovido.Value > (int)envio.Status)
        {
            envio.Status = promovido.Value;
            await PromoverPedidoAsync(envio, promovido.Value, cancellationToken);
        }

        await RegistrarEventosDeRastreioAsync(envio, rastreio, cancellationToken);

        envio.RawUltimaResposta = rastreio.RawJson;
        envio.DataAlteracao = _relogio.UtcNow;

        _envios.Atualizar(envio);
        await _unitOfWork.SaveChangesAsync(cancellationToken);
    }

    /// <inheritdoc />
    public async Task<bool> CancelarAsync(
        int idPedido,
        string? descricao = null,
        CancellationToken cancellationToken = default)
    {
        var existente = await _envios.ObterPorPedidoAsync(idPedido, cancellationToken);

        if (existente is null)
            return false;

        var cancelouNoParceiro = true;

        if (!string.IsNullOrWhiteSpace(existente.MeOrderId) && existente.Status != StatusEnvio.Cancelado)
        {
            // I/O de rede FORA de qualquer transacao: segurar lock de banco durante chamada ao
            // parceiro trava a expedicao inteira quando o Melhor Envio fica lento.
            var resultado = await _melhorEnvio.CancelarAsync(
                new CancelamentoEtiquetaRequisicao
                {
                    MeOrderId = existente.MeOrderId,
                    // O motivo e sempre "2" em integracao: e o generico de desistencia, o unico
                    // que o Melhor Envio aceita de quem nao e o usuario do painel dele.
                    MotivoId = "2",
                    Descricao = descricao
                },
                cancellationToken);

            cancelouNoParceiro = resultado.Sucesso;

            if (!cancelouNoParceiro)
            {
                _logger.LogWarning(
                    "Melhor Envio recusou o cancelamento da etiqueta {Etiqueta}: {Mensagem}",
                    existente.MeOrderId,
                    resultado.Mensagem);
            }
        }

        var envio = await _envios.ObterParaEdicaoAsync(existente.Id, cancellationToken);

        if (envio is null)
            return false;

        envio.Status = StatusEnvio.Cancelado;
        envio.ProximaTentativaEm = null;
        envio.DataAlteracao = _relogio.UtcNow;

        _envios.Atualizar(envio);

        await _envios.RegistrarEventoAsync(
            new EnvioEvento
            {
                IdEnvio = envio.Id,
                Status = StatusEnvio.Cancelado,
                Descricao = descricao ?? "Envio cancelado.",
                OcorridoEm = _relogio.UtcNow,
                RegistradoEm = _relogio.UtcNow
            },
            cancellationToken);

        await _unitOfWork.SaveChangesAsync(cancellationToken);

        return cancelouNoParceiro;
    }

    // ------------------------------------------------------------------
    // Passos da maquina de estados
    // ------------------------------------------------------------------

    /// <summary>Passo 1 — POST /api/cart. Pendente para NoCarrinho.</summary>
    private async Task InserirNoCarrinhoAsync(Envio envio, Pedido pedido, CancellationToken cancellationToken)
    {
        var resultado = await _melhorEnvio.InserirNoCarrinhoAsync(
            MontarRequisicaoDeCarrinho(envio, pedido), cancellationToken);

        // O MeOrderId e a chave de TUDO daqui pra frente. Gravado antes de qualquer outra coisa:
        // uma queda depois desta linha ainda permite retomar; antes dela, nao.
        envio.MeOrderId = resultado.MeOrderId;
        envio.Status = StatusEnvio.NoCarrinho;
        envio.RawUltimaResposta = resultado.RawJson;
        envio.UltimoErro = null;
        envio.DataAlteracao = _relogio.UtcNow;

        _envios.Atualizar(envio);

        await _envios.RegistrarEventoAsync(
            new EnvioEvento
            {
                IdEnvio = envio.Id,
                Status = StatusEnvio.NoCarrinho,
                Descricao = $"Etiqueta {resultado.MeOrderId} inserida no carrinho do Melhor Envio.",
                OcorridoEm = _relogio.UtcNow,
                RegistradoEm = _relogio.UtcNow
            },
            cancellationToken);

        await _unitOfWork.SaveChangesAsync(cancellationToken);
    }

    /// <summary>
    /// Passo 2 — POST /api/cart/checkout. NoCarrinho para Comprado.
    /// E a chamada que DEBITA a carteira do Melhor Envio: saldo insuficiente volta 4xx e o worker
    /// entra em backoff em vez de desistir, dando tempo do lojista recarregar.
    /// </summary>
    private async Task ComprarAsync(Envio envio, Pedido pedido, CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(envio.MeOrderId))
            throw new InvalidOperationException("Envio em NoCarrinho sem MeOrderId gravado.");

        var resultado = await _melhorEnvio.ComprarAsync([envio.MeOrderId], cancellationToken);

        if (!resultado.Sucesso)
            throw new InvalidOperationException(resultado.Mensagem ?? "Compra da etiqueta recusada.");

        envio.Status = StatusEnvio.Comprado;
        envio.ValorCompradoCentavos = resultado.ValoresPorEtiqueta.TryGetValue(envio.MeOrderId, out var valor)
            ? valor
            // Fallback para o cotado quando o parceiro nao detalha o custo por etiqueta: melhor um
            // valor aproximado no relatorio de margem do que uma coluna nula sem explicacao.
            : resultado.TotalCentavos ?? envio.ValorCotadoCentavos;
        envio.RawUltimaResposta = resultado.RawJson;
        envio.UltimoErro = null;
        envio.DataAlteracao = _relogio.UtcNow;

        _envios.Atualizar(envio);

        await _envios.RegistrarEventoAsync(
            new EnvioEvento
            {
                IdEnvio = envio.Id,
                Status = StatusEnvio.Comprado,
                Descricao = $"Etiqueta comprada no pedido {pedido.Numero}.",
                OcorridoEm = _relogio.UtcNow,
                RegistradoEm = _relogio.UtcNow
            },
            cancellationToken);

        await _unitOfWork.SaveChangesAsync(cancellationToken);
    }

    /// <summary>
    /// Passo 3 — POST /api/labels/generate, seguido do passo 4 (print).
    /// Falha no PRINT nao regride o status: a etiqueta ja foi paga e gerada, so o link faltou —
    /// ele e buscado sob demanda depois.
    /// </summary>
    private async Task GerarEtiquetaAsync(Envio envio, Pedido pedido, CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(envio.MeOrderId))
            throw new InvalidOperationException("Envio em Comprado sem MeOrderId gravado.");

        var geracao = await _melhorEnvio.GerarEtiquetaAsync([envio.MeOrderId], cancellationToken);

        if (!geracao.Gerada(envio.MeOrderId))
        {
            var mensagem = geracao.Itens.TryGetValue(envio.MeOrderId, out var item)
                ? item.Mensagem
                : null;

            throw new InvalidOperationException(mensagem ?? "Melhor Envio nao gerou a etiqueta.");
        }

        envio.Status = StatusEnvio.EtiquetaGerada;
        envio.RawUltimaResposta = geracao.RawJson;
        envio.UltimoErro = null;
        envio.DataAlteracao = _relogio.UtcNow;

        _envios.Atualizar(envio);

        await _envios.RegistrarEventoAsync(
            new EnvioEvento
            {
                IdEnvio = envio.Id,
                Status = StatusEnvio.EtiquetaGerada,
                Descricao = $"Etiqueta gerada para o pedido {pedido.Numero}.",
                OcorridoEm = _relogio.UtcNow,
                RegistradoEm = _relogio.UtcNow
            },
            cancellationToken);

        await _unitOfWork.SaveChangesAsync(cancellationToken);

        try
        {
            var impressao = await _melhorEnvio.ImprimirEtiquetaAsync(
                [envio.MeOrderId], ModoImpressaoEtiqueta.Privado, cancellationToken);

            if (!string.IsNullOrWhiteSpace(impressao.Url))
            {
                envio.UrlEtiqueta = impressao.Url;
                _envios.Atualizar(envio);
                await _unitOfWork.SaveChangesAsync(cancellationToken);
            }
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
            throw;
        }
        catch (Exception excecao)
        {
            _logger.LogWarning(
                excecao,
                "Etiqueta {Etiqueta} gerada, mas sem link de impressao. O link sera buscado sob demanda.",
                envio.MeOrderId);
        }

        await PromoverPedidoParaSeparacaoAsync(pedido.Id, cancellationToken);
    }

    // ------------------------------------------------------------------
    // Montagem do payload
    // ------------------------------------------------------------------

    private CarrinhoEnvioRequisicao MontarRequisicaoDeCarrinho(Envio envio, Pedido pedido)
    {
        var entrega = pedido.EnderecoEntrega;
        var remetente = _frete.Remetente;

        var pesoTotalKg = pedido.PesoTotalGramas > 0
            ? pedido.PesoTotalGramas / 1000m
            : _frete.VolumeFallback.PesoGramas / 1000m;

        return new CarrinhoEnvioRequisicao
        {
            IdServico = envio.IdServico,
            Remetente = new ParteEnvioInfo
            {
                Nome = remetente.Nome,
                Email = remetente.Email,
                Telefone = TelefoneHelper.SomenteDigitos(remetente.Telefone),
                DocumentoEmpresa = DocumentoHelper.SomenteDigitos(remetente.Documento),
                InscricaoEstadual = remetente.InscricaoEstadual,
                CodigoAtividadeEconomica = remetente.CodigoAtividadeEconomica,
                Logradouro = remetente.Logradouro,
                Numero = remetente.Numero,
                Complemento = remetente.Complemento,
                Bairro = remetente.Bairro,
                Cidade = remetente.Cidade,
                Cep = CepHelper.SomenteDigitos(_frete.CepOrigem),
                Uf = remetente.Uf.ToUpperInvariant()
            },
            Destinatario = new ParteEnvioInfo
            {
                Nome = entrega.Destinatario,
                Telefone = entrega.TelefoneContato,
                Documento = entrega.DocumentoDestinatario,
                Logradouro = entrega.Logradouro,
                Numero = entrega.Numero,
                Complemento = entrega.Complemento,
                // Nunca vazio: district vazio e recusa imediata do Melhor Envio.
                Bairro = entrega.Bairro,
                Cidade = entrega.Cidade,
                Cep = entrega.Cep,
                Uf = entrega.Uf.ToUpperInvariant()
            },
            Produtos =
            [
                .. pedido.Itens.Select(item => new ProdutoDeclaradoInfo
                {
                    Nome = $"{item.NomeProdutoSnapshot} - {item.TamanhoSnapshot} / {item.CorSnapshot}",
                    Quantidade = item.Quantidade,
                    ValorUnitarioCentavos = item.PrecoUnitarioCentavos,
                    PesoKg = item.PesoGramasSnapshot / 1000m
                })
            ],
            // UM volume por pedido, com as dimensoes da caixa padrao. Um volume por item faria o
            // Melhor Envio cobrar como se fossem varias encomendas (armadilha #4).
            Volumes =
            [
                new VolumeEnvioInfo
                {
                    AlturaCm = _frete.VolumeFallback.AlturaCm,
                    LarguraCm = _frete.VolumeFallback.LarguraCm,
                    ComprimentoCm = _frete.VolumeFallback.ComprimentoCm,
                    PesoKg = pesoTotalKg
                }
            ],
            Opcoes = new OpcoesEnvioInfo
            {
                Plataforma = _frete.Plataforma,
                ValorSeguradoCentavos = pedido.SubtotalCentavos,
                AvisoRecebimento = false,
                MaoPropria = false,
                Reversa = false,
                // Sem chave de nota, o envio vai como nao comercial. Com chave, e comercial e o
                // campo precisa ir como false explicito.
                NaoComercial = string.IsNullOrWhiteSpace(envio.ChaveNfe),
                ChaveNfe = envio.ChaveNfe,
                Tags =
                [
                    new EtiquetaTagInfo
                    {
                        // O numero do pedido e o que casa a etiqueta com o pedido nas telas do
                        // Melhor Envio quando alguem precisa investigar manualmente.
                        Tag = pedido.Numero,
                        Url = _app.UrlLoja($"admin/pedidos/{pedido.Numero}")
                    }
                ]
            }
        };
    }

    // ------------------------------------------------------------------
    // Falha e apoio
    // ------------------------------------------------------------------

    private async Task RegistrarFalhaAsync(Envio envio, Exception excecao, CancellationToken cancellationToken)
    {
        var agora = _relogio.UtcNow;

        envio.UltimoErro = Truncar(excecao.Message, LimiteUltimoErro);
        envio.DataAlteracao = agora;

        if (EnvioRetryPolicy.EsgotouTentativas(envio.Tentativas))
        {
            envio.Status = StatusEnvio.Falha;
            envio.ProximaTentativaEm = null;

            _logger.LogError(
                excecao,
                "Envio {Envio} esgotou as {Maximo} tentativas e foi para Falha.",
                envio.Id,
                EnvioRetryPolicy.MaximoTentativas);

            await AlertarAdministracaoAsync(envio, cancellationToken);
        }
        else
        {
            // O claim ja incrementou Tentativas; o backoff e calculado sobre o numero novo.
            envio.ProximaTentativaEm = EnvioRetryPolicy.ProximaTentativa(agora, envio.Tentativas);

            _logger.LogWarning(
                excecao,
                "Envio {Envio} falhou na tentativa {Tentativa}. Proxima em {Proxima}.",
                envio.Id,
                envio.Tentativas,
                envio.ProximaTentativaEm);
        }

        _envios.Atualizar(envio);

        await _envios.RegistrarEventoAsync(
            new EnvioEvento
            {
                IdEnvio = envio.Id,
                Status = envio.Status,
                Descricao = envio.UltimoErro,
                OcorridoEm = agora,
                RegistradoEm = agora
            },
            cancellationToken);

        await _unitOfWork.SaveChangesAsync(cancellationToken);
    }

    /// <summary>
    /// Dado inconsistente nao entra em retry: oito tentativas nao vao fazer aparecer um endereco
    /// que nunca foi gravado, e enquanto isso a fila anda mais devagar para todo mundo.
    /// </summary>
    private async Task MarcarFalhaDefinitivaAsync(Envio envio, string motivo, CancellationToken cancellationToken)
    {
        var agora = _relogio.UtcNow;

        envio.Status = StatusEnvio.Falha;
        envio.UltimoErro = Truncar(motivo, LimiteUltimoErro);
        envio.ProximaTentativaEm = null;
        envio.DataAlteracao = agora;

        _envios.Atualizar(envio);

        await _envios.RegistrarEventoAsync(
            new EnvioEvento
            {
                IdEnvio = envio.Id,
                Status = StatusEnvio.Falha,
                Descricao = envio.UltimoErro,
                OcorridoEm = agora,
                RegistradoEm = agora
            },
            cancellationToken);

        await _unitOfWork.SaveChangesAsync(cancellationToken);

        _logger.LogError("Envio {Envio} marcado como Falha sem retry: {Motivo}", envio.Id, motivo);

        await AlertarAdministracaoAsync(envio, cancellationToken);
    }

    private async Task PromoverPedidoParaSeparacaoAsync(int idPedido, CancellationToken cancellationToken)
    {
        var pedido = await _pedidos.ObterParaEdicaoAsync(idPedido, cancellationToken);

        if (pedido is null || pedido.Status != StatusPedido.Pago)
            return;

        pedido.Status = StatusPedido.EmSeparacao;
        _pedidos.Atualizar(pedido);

        await _pedidos.RegistrarHistoricoAsync(
            new PedidoHistorico
            {
                IdPedido = pedido.Id,
                StatusAnterior = StatusPedido.Pago,
                StatusNovo = StatusPedido.EmSeparacao,
                IdUsuario = null,
                Observacao = "Etiqueta gerada automaticamente.",
                DataAlteracao = _relogio.UtcNow
            },
            cancellationToken);

        await _unitOfWork.SaveChangesAsync(cancellationToken);
    }

    private async Task PromoverPedidoAsync(Envio envio, StatusEnvio statusEnvio, CancellationToken cancellationToken)
    {
        var alvo = statusEnvio switch
        {
            StatusEnvio.Postado => StatusPedido.Enviado,
            StatusEnvio.Entregue => StatusPedido.Entregue,
            _ => (StatusPedido?)null
        };

        if (alvo is null)
            return;

        var pedido = await _pedidos.ObterParaEdicaoAsync(envio.IdPedido, cancellationToken);

        if (pedido is null || (int)pedido.Status >= (int)alvo.Value || pedido.Status == StatusPedido.Cancelado)
            return;

        var anterior = pedido.Status;
        pedido.Status = alvo.Value;

        if (alvo == StatusPedido.Enviado)
            pedido.DataEnvio = _relogio.UtcNow;
        else
            pedido.DataEntrega = _relogio.UtcNow;

        _pedidos.Atualizar(pedido);

        await _pedidos.RegistrarHistoricoAsync(
            new PedidoHistorico
            {
                IdPedido = pedido.Id,
                StatusAnterior = anterior,
                StatusNovo = alvo.Value,
                IdUsuario = null,
                Observacao = "Atualizacao automatica pelo rastreio do Melhor Envio.",
                DataAlteracao = _relogio.UtcNow
            },
            cancellationToken);
    }

    /// <summary>
    /// Grava apenas os eventos ainda desconhecidos. O Melhor Envio reenvia a timeline inteira a
    /// cada consulta; sem a comparacao, cada ciclo do worker duplicaria o historico do cliente.
    /// </summary>
    private async Task RegistrarEventosDeRastreioAsync(
        Envio envio,
        RastreioResultado rastreio,
        CancellationToken cancellationToken)
    {
        if (rastreio.Eventos.Count == 0)
            return;

        var existentes = await _envios.ObterEventosAsync(envio.Id, cancellationToken);

        var conhecidos = existentes
            .Select(evento => $"{evento.OcorridoEm:O}|{evento.Descricao}")
            .ToHashSet(StringComparer.Ordinal);

        foreach (var evento in rastreio.Eventos)
        {
            var ocorridoEm = evento.DataUtc ?? _relogio.UtcNow;
            var chave = $"{ocorridoEm:O}|{evento.Descricao}";

            if (!conhecidos.Add(chave))
                continue;

            await _envios.RegistrarEventoAsync(
                new EnvioEvento
                {
                    IdEnvio = envio.Id,
                    Status = rastreio.StatusEquivalente ?? envio.Status,
                    Descricao = evento.Descricao,
                    Local = evento.Local,
                    OcorridoEm = ocorridoEm,
                    RegistradoEm = _relogio.UtcNow
                },
                cancellationToken);
        }
    }

    private async Task AlertarAdministracaoAsync(Envio envio, CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(_app.EmailAdministrativo))
            return;

        try
        {
            await _email.EnviarAsync(
                _app.EmailAdministrativo,
                $"{_app.NomeLoja} - falha na etiqueta do envio {envio.Id}",
                $"<p>O envio {envio.Id} (pedido interno {envio.IdPedido}) parou em " +
                $"{envio.Status}.</p><p>Ultimo erro: {envio.UltimoErro}</p>",
                cancellationToken);
        }
        catch (Exception excecao)
        {
            _logger.LogWarning(excecao, "Falha ao alertar a administracao sobre o envio {Envio}.", envio.Id);
        }
    }

    private static string Truncar(string? valor, int limite)
    {
        if (string.IsNullOrEmpty(valor))
            return string.Empty;

        return valor.Length <= limite ? valor : valor[..limite];
    }
}
