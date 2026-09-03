using System.ComponentModel.DataAnnotations;
using Glorific.Domain.Helpers;

namespace Glorific.Application.DTO.Conta;

/// <summary>
/// Endereco de entrega do cliente. Os limites de tamanho espelham exatamente as colunas de
/// enderecos: validar aqui devolve 400 com o campo culpado, e nao um 500 do driver do Postgres
/// reclamando de "value too long for type character varying".
///
/// IdUsuario NAO existe neste DTO: o dono sai do token. Aceitar o dono pelo corpo e o caminho
/// mais curto para um cliente cadastrar endereco na conta de outro.
/// </summary>
public sealed record EnderecoCreateDto : CreateDto
{
    [StringLength(60, ErrorMessage = "Apelido longo demais.")]
    public string? Apelido { get; init; }

    [Required(ErrorMessage = "Informe o destinatario.")]
    [StringLength(180, MinimumLength = 2, ErrorMessage = "Destinatario invalido.")]
    public string Destinatario { get; init; } = string.Empty;

    /// <summary>CPF/CNPJ do destinatario. A transportadora exige documento para emitir etiqueta.</summary>
    [StringLength(18, ErrorMessage = "Documento invalido.")]
    public string? DocumentoDestinatario { get; init; }

    [Required(ErrorMessage = "Informe o telefone de contato.")]
    [StringLength(20, ErrorMessage = "Telefone invalido.")]
    public string TelefoneContato { get; init; } = string.Empty;

    [Required(ErrorMessage = "Informe o CEP.")]
    [StringLength(9, MinimumLength = 8, ErrorMessage = "CEP invalido.")]
    public string Cep { get; init; } = string.Empty;

    [Required(ErrorMessage = "Informe o logradouro.")]
    [StringLength(200, ErrorMessage = "Logradouro longo demais.")]
    public string Logradouro { get; init; } = string.Empty;

    [Required(ErrorMessage = "Informe o numero.")]
    [StringLength(20, ErrorMessage = "Numero longo demais.")]
    public string Numero { get; init; } = string.Empty;

    [StringLength(120, ErrorMessage = "Complemento longo demais.")]
    public string? Complemento { get; init; }

    /// <summary>Obrigatorio: POST /api/cart do Melhor Envio exige district.</summary>
    [Required(ErrorMessage = "Informe o bairro.")]
    [StringLength(120, ErrorMessage = "Bairro longo demais.")]
    public string Bairro { get; init; } = string.Empty;

    [Required(ErrorMessage = "Informe a cidade.")]
    [StringLength(120, ErrorMessage = "Cidade longa demais.")]
    public string Cidade { get; init; } = string.Empty;

    [Required(ErrorMessage = "Informe a UF.")]
    [StringLength(2, MinimumLength = 2, ErrorMessage = "UF precisa ter 2 letras.")]
    public string Uf { get; init; } = string.Empty;

    /// <summary>Marca este endereco como principal ao criar.</summary>
    public bool Principal { get; init; }
}

/// <summary>
/// Atualizacao de endereco. Id vem da rota, dono vem do token — nenhum dos dois pelo corpo.
/// Principal fica de fora: promover a principal tem endpoint proprio porque o efeito e sobre
/// os OUTROS enderecos do cliente (so pode existir um), nao sobre este.
/// </summary>
public sealed record EnderecoUpdateDto : UpdateDto
{
    [StringLength(60, ErrorMessage = "Apelido longo demais.")]
    public string? Apelido { get; init; }

    [Required(ErrorMessage = "Informe o destinatario.")]
    [StringLength(180, MinimumLength = 2, ErrorMessage = "Destinatario invalido.")]
    public string Destinatario { get; init; } = string.Empty;

    [StringLength(18, ErrorMessage = "Documento invalido.")]
    public string? DocumentoDestinatario { get; init; }

    [Required(ErrorMessage = "Informe o telefone de contato.")]
    [StringLength(20, ErrorMessage = "Telefone invalido.")]
    public string TelefoneContato { get; init; } = string.Empty;

    [Required(ErrorMessage = "Informe o CEP.")]
    [StringLength(9, MinimumLength = 8, ErrorMessage = "CEP invalido.")]
    public string Cep { get; init; } = string.Empty;

    [Required(ErrorMessage = "Informe o logradouro.")]
    [StringLength(200, ErrorMessage = "Logradouro longo demais.")]
    public string Logradouro { get; init; } = string.Empty;

    [Required(ErrorMessage = "Informe o numero.")]
    [StringLength(20, ErrorMessage = "Numero longo demais.")]
    public string Numero { get; init; } = string.Empty;

    [StringLength(120, ErrorMessage = "Complemento longo demais.")]
    public string? Complemento { get; init; }

    [Required(ErrorMessage = "Informe o bairro.")]
    [StringLength(120, ErrorMessage = "Bairro longo demais.")]
    public string Bairro { get; init; } = string.Empty;

    [Required(ErrorMessage = "Informe a cidade.")]
    [StringLength(120, ErrorMessage = "Cidade longa demais.")]
    public string Cidade { get; init; } = string.Empty;

    [Required(ErrorMessage = "Informe a UF.")]
    [StringLength(2, MinimumLength = 2, ErrorMessage = "UF precisa ter 2 letras.")]
    public string Uf { get; init; } = string.Empty;
}

/// <summary>Endereco como sai da API.</summary>
public sealed record EnderecoResponseDto : ResponseDto
{
    public int Id { get; init; }

    public string? Apelido { get; init; }

    public string Destinatario { get; init; } = string.Empty;

    public string? DocumentoDestinatario { get; init; }

    public string TelefoneContato { get; init; } = string.Empty;

    /// <summary>Oito digitos, sem mascara — e assim que fica gravado e que o frete e cotado.</summary>
    public string Cep { get; init; } = string.Empty;

    public string Logradouro { get; init; } = string.Empty;
    public string Numero { get; init; } = string.Empty;
    public string? Complemento { get; init; }
    public string Bairro { get; init; } = string.Empty;
    public string Cidade { get; init; } = string.Empty;
    public string Uf { get; init; } = string.Empty;
    public string Pais { get; init; } = "BR";
    public bool Principal { get; init; }

    /// <summary>
    /// Somente leitura, derivado. Poupa o front de reimplementar a mascara e garante que a
    /// exibicao nunca divirja do valor que foi de fato usado na cotacao de frete.
    /// </summary>
    public string CepFormatado => CepHelper.Formatar(Cep);
}
