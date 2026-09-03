namespace Glorific.Domain.Exceptions;

/// <summary>Recurso inexistente. O middleware traduz para 404.</summary>
public class EntityNotFoundException : Exception
{
    public EntityNotFoundException(string mensagem) : base(mensagem) { }

    public EntityNotFoundException(string entidade, object chave)
        : base($"{entidade} de identificador '{chave}' nao foi encontrado.") { }
}
