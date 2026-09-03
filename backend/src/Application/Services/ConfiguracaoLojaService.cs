using Glorific.Application.DTO.Config;
using Glorific.Application.Exceptions;
using Glorific.Application.Services.Interfaces;
using Glorific.Domain.Entities.Config;
using Glorific.Domain.Exceptions;
using Glorific.Domain.Helpers;
using Glorific.Domain.Interfaces;
using Glorific.Domain.Interfaces.Repositories;
using MapsterMapper;
using Microsoft.Extensions.Caching.Memory;

namespace Glorific.Application.Services;

/// <summary>
/// Configuracao da loja com leitura cacheada.
///
/// O que fica no cache e o DTO, nunca a entidade. Guardar a entidade rastreada num cache de
/// processo entregaria a mesma instancia (e, por tabela, o DbContext que a rastreia) para
/// requisicoes concorrentes de clientes diferentes — o vazamento e silencioso e so aparece como
/// erro de concorrencia do EF em producao. O record e imutavel e nao tem dono.
///
/// A expiracao por tempo existe como rede de seguranca para alteracao feita por fora (seed,
/// script de suporte); o caminho normal e a invalidacao explicita no save.
/// </summary>
public class ConfiguracaoLojaService : IConfiguracaoLojaService
{
    /// <summary>Chave unica no cache do processo. Prefixada para nao colidir com outro consumidor.</summary>
    private const string ChaveCache = "glorific:configuracao-loja";

    private static readonly TimeSpan ValidadeCache = TimeSpan.FromMinutes(10);

    private readonly IConfiguracaoLojaRepository _configuracoes;
    private readonly IUnitOfWork _unitOfWork;
    private readonly IMapper _mapper;
    private readonly IMemoryCache _cache;

    public ConfiguracaoLojaService(
        IConfiguracaoLojaRepository configuracoes,
        IUnitOfWork unitOfWork,
        IMapper mapper,
        IMemoryCache cache)
    {
        _configuracoes = configuracoes ?? throw new ArgumentNullException(nameof(configuracoes));
        _unitOfWork = unitOfWork ?? throw new ArgumentNullException(nameof(unitOfWork));
        _mapper = mapper ?? throw new ArgumentNullException(nameof(mapper));
        _cache = cache ?? throw new ArgumentNullException(nameof(cache));
    }

    /// <inheritdoc />
    public async Task<ConfiguracaoLojaResponseDto> ObterAsync(CancellationToken cancellationToken = default)
    {
        if (_cache.TryGetValue(ChaveCache, out ConfiguracaoLojaResponseDto? cacheado) && cacheado is not null)
            return cacheado;

        var configuracao = await _configuracoes.ObterAsync(cancellationToken)
            ?? throw new EntityNotFoundException(
                "A configuracao da loja ainda nao foi criada. Rode o seed inicial ou salve a configuracao pelo painel.");

        var dto = _mapper.Map<ConfiguracaoLojaResponseDto>(configuracao);

        _cache.Set(ChaveCache, dto, new MemoryCacheEntryOptions
        {
            AbsoluteExpirationRelativeToNow = ValidadeCache,
            // Tamanho declarado para o caso de alguem configurar SizeLimit no cache compartilhado:
            // sem Size, o Set lanca quando o limite existe. Uma linha de configuracao pesa 1.
            Size = 1
        });

        return dto;
    }

    /// <inheritdoc />
    public async Task<ConfiguracaoLojaResponseDto> AtualizarAsync(
        ConfiguracaoLojaUpdateDto dto,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(dto);

        var cep = CepHelper.SomenteDigitos(dto.CepOrigem);

        // O CEP de origem entra em toda cotacao do Melhor Envio. Um digito a menos aqui derruba a
        // cotacao da loja inteira, e o erro aparece como "frete indisponivel" para o cliente.
        if (!CepHelper.Valido(cep))
            throw new BusinessValidationException("CEP de origem invalido.");

        if (dto.FreteGratisAcimaDeCentavos is { } freteGratis
            && dto.PedidoMinimoCentavos is { } pedidoMinimo
            && freteGratis < pedidoMinimo)
        {
            throw new BusinessValidationException(
                "O valor de frete gratis nao pode ser menor que o pedido minimo.");
        }

        var configuracao = await _configuracoes.ObterParaEdicaoAsync(cancellationToken);

        if (configuracao is null)
        {
            // Instalacao nova em que o seed nao rodou: criar aqui e melhor que devolver 404 para o
            // admin que esta justamente tentando configurar a loja pela primeira vez.
            // DataCriacao nao e preenchida aqui: ConfiguracaoLoja e IAuditable e o carimbo vem do
            // proprio DbContext, com o mesmo IClock de todo o resto.
            configuracao = new ConfiguracaoLoja { CepOrigem = cep };

            _mapper.Map(dto, configuracao);
            configuracao.CepOrigem = cep;

            await _configuracoes.AdicionarAsync(configuracao, cancellationToken);
        }
        else
        {
            _mapper.Map(dto, configuracao);
            configuracao.CepOrigem = cep;
        }

        // Quem salva e o caso de uso. O repositorio so registrou a intencao.
        await _unitOfWork.SaveChangesAsync(cancellationToken);

        InvalidarCache();

        var atualizado = _mapper.Map<ConfiguracaoLojaResponseDto>(configuracao);

        _cache.Set(ChaveCache, atualizado, new MemoryCacheEntryOptions
        {
            AbsoluteExpirationRelativeToNow = ValidadeCache,
            Size = 1
        });

        return atualizado;
    }

    /// <inheritdoc />
    public void InvalidarCache() => _cache.Remove(ChaveCache);
}
