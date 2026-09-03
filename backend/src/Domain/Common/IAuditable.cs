namespace Glorific.Domain.Common;

/// <summary>Entidade que carrega carimbo de criacao/alteracao, preenchido pelo DbContext.</summary>
public interface IAuditable
{
    DateTime DataCriacao { get; set; }
    DateTime? DataAlteracao { get; set; }
}
