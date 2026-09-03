namespace Glorific.Application.DTO;

/// <summary>
/// Marcador de DTO de CRIACAO. Concretos: public sealed record ProdutoCreateDto : CreateDto
/// com propriedades { get; init; } e DataAnnotations.
///
/// Por que markers vazios e nao classes com campos: o generico
/// GenericService&lt;TEntity, TCreate, TUpdate, TResponse&gt; precisa de uma restricao que
/// impeca passar a entidade crua como DTO, sem impor nenhuma propriedade comum — Id nao existe
/// no create, e no response ele nem sempre e int (Uuid publico em usuario e pedido).
/// </summary>
public abstract record CreateDto;

/// <summary>
/// Marcador de DTO de ATUALIZACAO. O Id NAO mora aqui: vem da rota (PUT /produtos/{id}), senao
/// o corpo pode contradizer a URL e alguem acaba atualizando outro registro.
/// </summary>
public abstract record UpdateDto;

/// <summary>
/// Marcador de DTO ENXUTO, para listas e combos (id + rotulo + o minimo de contexto).
/// Serve para nao devolver o agregado inteiro numa listagem de 100 linhas.
/// </summary>
public abstract record SimpleDto;

/// <summary>
/// Marcador de DTO de RESPOSTA. O controller generico expoe
/// protected abstract int GetId(TResponseDto dto) — a extracao do id e explicita por controller,
/// nunca por reflection como no repo de referencia.
/// </summary>
public abstract record ResponseDto;
