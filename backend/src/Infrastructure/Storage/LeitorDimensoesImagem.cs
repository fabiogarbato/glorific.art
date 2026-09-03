using System.Buffers.Binary;

namespace Glorific.Infrastructure.Storage;

/// <summary>
/// Le largura e altura direto do CABECALHO do arquivo, sem decodificar a imagem.
///
/// Por que isso existe em vez de uma biblioteca de imagem: o front precisa da proporcao para
/// reservar o espaco da foto ANTES do download — em vitrine de moda a imagem e o produto, e
/// layout pulando enquanto carrega custa conversao. Decodificar o bitmap inteiro so para
/// descobrir dois numeros custaria memoria proporcional a resolucao em cada upload.
///
/// Formato desconhecido ou cabecalho truncado devolve (0, 0). Nunca lanca: dimensao ausente e
/// um detalhe de layout, e derrubar um upload valido por causa disso seria desproporcional.
/// </summary>
internal static class LeitorDimensoesImagem
{
    /// <summary>Cabecalho suficiente para PNG, GIF, WEBP e para varrer os segmentos do JPEG.</summary>
    private const int BytesDeCabecalho = 64 * 1024;

    public static (int Largura, int Altura) Ler(byte[] conteudo)
    {
        if (conteudo is null || conteudo.Length < 16)
            return (0, 0);

        var dados = conteudo.AsSpan(0, Math.Min(conteudo.Length, BytesDeCabecalho));

        if (EhPng(dados))
            return LerPng(dados);

        if (EhGif(dados))
            return LerGif(dados);

        if (EhWebp(dados))
            return LerWebp(dados);

        if (EhJpeg(dados))
            return LerJpeg(dados);

        return (0, 0);
    }

    private static bool EhPng(ReadOnlySpan<byte> d) =>
        d.Length > 24 && d[0] == 0x89 && d[1] == 0x50 && d[2] == 0x4E && d[3] == 0x47;

    /// <summary>IHDR e sempre o primeiro chunk: largura e altura em big-endian nos offsets 16 e 20.</summary>
    private static (int, int) LerPng(ReadOnlySpan<byte> d) =>
        (
            BinaryPrimitives.ReadInt32BigEndian(d.Slice(16, 4)),
            BinaryPrimitives.ReadInt32BigEndian(d.Slice(20, 4))
        );

    private static bool EhGif(ReadOnlySpan<byte> d) =>
        d.Length > 10 && d[0] == 0x47 && d[1] == 0x49 && d[2] == 0x46;

    private static (int, int) LerGif(ReadOnlySpan<byte> d) =>
        (
            BinaryPrimitives.ReadUInt16LittleEndian(d.Slice(6, 2)),
            BinaryPrimitives.ReadUInt16LittleEndian(d.Slice(8, 2))
        );

    private static bool EhWebp(ReadOnlySpan<byte> d) =>
        d.Length > 30
        && d[0] == 0x52 && d[1] == 0x49 && d[2] == 0x46 && d[3] == 0x46
        && d[8] == 0x57 && d[9] == 0x45 && d[10] == 0x42 && d[11] == 0x50;

    /// <summary>
    /// WEBP tem tres variantes de chunk. VP8X e VP8L guardam a dimensao empacotada em bits;
    /// VP8 (lossy) guarda em 14 bits apos o start code de tres bytes.
    /// </summary>
    private static (int, int) LerWebp(ReadOnlySpan<byte> d)
    {
        var tipo = System.Text.Encoding.ASCII.GetString(d.Slice(12, 4));

        switch (tipo)
        {
            case "VP8X" when d.Length > 30:
            {
                var largura = 1 + (d[24] | (d[25] << 8) | (d[26] << 16));
                var altura = 1 + (d[27] | (d[28] << 8) | (d[29] << 16));
                return (largura, altura);
            }

            case "VP8L" when d.Length > 25:
            {
                var bits = BinaryPrimitives.ReadUInt32LittleEndian(d.Slice(21, 4));
                var largura = (int)((bits & 0x3FFF) + 1);
                var altura = (int)(((bits >> 14) & 0x3FFF) + 1);
                return (largura, altura);
            }

            case "VP8 " when d.Length > 30:
            {
                var largura = BinaryPrimitives.ReadUInt16LittleEndian(d.Slice(26, 2)) & 0x3FFF;
                var altura = BinaryPrimitives.ReadUInt16LittleEndian(d.Slice(28, 2)) & 0x3FFF;
                return (largura, altura);
            }

            default:
                return (0, 0);
        }
    }

    private static bool EhJpeg(ReadOnlySpan<byte> d) =>
        d.Length > 4 && d[0] == 0xFF && d[1] == 0xD8;

    /// <summary>
    /// Percorre os segmentos ate um Start Of Frame (SOF0..SOF15, exceto os marcadores 0xC4,
    /// 0xC8 e 0xCC, que nao sao SOF). E dele que saem altura e largura.
    /// </summary>
    private static (int, int) LerJpeg(ReadOnlySpan<byte> d)
    {
        var posicao = 2;

        while (posicao + 9 < d.Length)
        {
            if (d[posicao] != 0xFF)
            {
                posicao++;
                continue;
            }

            var marcador = d[posicao + 1];

            // Preenchimento (0xFF repetido) e marcadores sem payload.
            if (marcador == 0xFF)
            {
                posicao++;
                continue;
            }

            if (marcador is 0xD8 or 0x01 || (marcador >= 0xD0 && marcador <= 0xD7))
            {
                posicao += 2;
                continue;
            }

            var tamanho = BinaryPrimitives.ReadUInt16BigEndian(d.Slice(posicao + 2, 2));

            if (tamanho < 2)
                return (0, 0);

            var ehSof = marcador >= 0xC0 && marcador <= 0xCF && marcador is not (0xC4 or 0xC8 or 0xCC);

            if (ehSof && posicao + 9 < d.Length)
            {
                var altura = BinaryPrimitives.ReadUInt16BigEndian(d.Slice(posicao + 5, 2));
                var largura = BinaryPrimitives.ReadUInt16BigEndian(d.Slice(posicao + 7, 2));
                return (largura, altura);
            }

            posicao += 2 + tamanho;
        }

        return (0, 0);
    }
}
