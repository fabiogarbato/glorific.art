namespace Glorific.Application.Ports;

/// <summary>
/// Porta de LEITURA de bytes de uma mídia já armazenada, pelo PublicId. Existe separada de
/// IImageStorage porque aquela porta só sabe ESCREVER (upload) e REMOVER — ler o arquivo pra
/// mandar pra outro serviço (aqui, um provedor de IA) é uma preocupação diferente.
/// </summary>
public interface ILeitorArquivoMidia
{
    Task<byte[]> LerBytesAsync(string publicId, CancellationToken ct = default);
}
