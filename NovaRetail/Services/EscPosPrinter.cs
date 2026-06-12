using System.Globalization;
using System.Net.Sockets;
using System.Text;

namespace NovaRetail.Services;

public static class EscPosPrinter
{
    private static readonly TimeSpan Timeout = TimeSpan.FromSeconds(5);

    public static async Task SendAsync(string host, int port, byte[] printJob)
    {
        using var timeout = new CancellationTokenSource(Timeout);
        using var client = new TcpClient();

        await client.ConnectAsync(host, port, timeout.Token);
        await using var stream = client.GetStream();
        await stream.WriteAsync(printJob, timeout.Token);
        await stream.FlushAsync(timeout.Token);
    }

    public static byte[] BuildPrintJob(Receipt receipt, int paperWidth = 48)
    {
        using var job = new MemoryStream();

        WriteBytes(job, 0x1B, 0x40);
        WriteBytes(job, 0x1B, 0x61, 0x00);
        WriteBytes(job, 0x1B, 0x21, 0x00);
        WriteText(job, ReceiptRenderer.BuildText(receipt, paperWidth));

        WriteBytes(job, 0x1B, 0x61, 0x01);
        WriteQrCode(job, ReceiptRenderer.QrData(receipt));
        WriteText(job, "\n");
        WriteBytes(job, 0x1B, 0x64, 0x04);
        WriteBytes(job, 0x1D, 0x56, 0x00);

        return job.ToArray();
    }

    private static void WriteQrCode(Stream stream, string value)
    {
        var data = Encoding.ASCII.GetBytes(ToPrinterText(value));
        if (data.Length > 7089)
            throw new InvalidDataException("El contenido del codigo QR supera el limite ESC/POS.");

        WriteBytes(stream, 0x1D, 0x28, 0x6B, 0x04, 0x00, 0x31, 0x41, 0x32, 0x00);
        WriteBytes(stream, 0x1D, 0x28, 0x6B, 0x03, 0x00, 0x31, 0x43, 0x05);
        WriteBytes(stream, 0x1D, 0x28, 0x6B, 0x03, 0x00, 0x31, 0x45, 0x31);

        var payloadLength = data.Length + 3;
        WriteBytes(
            stream,
            0x1D, 0x28, 0x6B,
            (byte)(payloadLength % 256),
            (byte)(payloadLength / 256),
            0x31, 0x50, 0x30);
        stream.Write(data);
        WriteBytes(stream, 0x1D, 0x28, 0x6B, 0x03, 0x00, 0x31, 0x51, 0x30);
    }

    private static void WriteText(Stream stream, string value) =>
        stream.Write(Encoding.ASCII.GetBytes(ToPrinterText(value)));

    private static string ToPrinterText(string value)
    {
        var decomposed = value.Normalize(NormalizationForm.FormD);
        var text = new StringBuilder(decomposed.Length);

        foreach (var character in decomposed)
        {
            if (CharUnicodeInfo.GetUnicodeCategory(character) == UnicodeCategory.NonSpacingMark)
                continue;

            text.Append(character switch
            {
                '\u2014' or '\u2013' => '-',
                '\u201c' or '\u201d' => '"',
                '\u2018' or '\u2019' => '\'',
                '\u20a1' => "CRC ",
                _ when character <= 0x7F => character,
                _ => '?'
            });
        }

        return text.ToString();
    }

    private static void WriteBytes(Stream stream, params byte[] bytes) =>
        stream.Write(bytes);
}
