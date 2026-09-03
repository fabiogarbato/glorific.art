namespace Glorific.Domain.Common;

/// <summary>Raiz de toda entidade persistida. PK int identity, herdada do padrao do cwbmaq.</summary>
public abstract class BaseEntity
{
    public int Id { get; set; }
}
