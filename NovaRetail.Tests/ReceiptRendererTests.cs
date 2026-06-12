using NovaRetail.Services;

namespace NovaRetail.Tests;

public sealed class ReceiptRendererTests
{
    [Fact]
    public void Builds_shared_receipt_html_text_and_escpos_job()
    {
        var receipt = new Receipt(
            new CompanyInfo("Nova Retail", "3-101-123456", "San Jose", "2222-2222"),
            new CustomerInfo("Cliente Prueba", "1-1111-1111"),
            "FACTURA ELECTRONICA",
            "00100001010000000001",
            "50601012600310112345600100001010000000001199999999",
            new DateTime(2026, 6, 11, 10, 30, 0),
            "1",
            "Maria",
            "CRC",
            new[]
            {
                new ReceiptItem("ART-001", "Producto de prueba", 2m, 1000m, 10m, 200m, 13m, 234m)
            },
            new[] { new ReceiptPayment("Efectivo", 2500m) },
            new[] { "Gracias por su compra" },
            "Documento generado por NovaRetail",
            2000m,
            200m,
            234m,
            2034m);

        var text = ReceiptRenderer.BuildText(receipt);
        var html = ReceiptRenderer.BuildHtml(receipt);
        var job = EscPosPrinter.BuildPrintJob(receipt);

        Assert.Contains("FACTURA ELECTRONICA", text);
        Assert.Contains("ART-001 - Producto de prueba", text);
        Assert.Contains("CRC 2,034", text);
        Assert.Contains("Vista previa del comprobante", html);
        Assert.Contains("50601012600310112345600100001010000000001199999999", html);
        Assert.Equal(new byte[] { 0x1B, 0x40 }, job.Take(2).ToArray());
        Assert.True(ContainsSequence(job, new byte[] { 0x1D, 0x28, 0x6B }));
        Assert.Equal(new byte[] { 0x1D, 0x56, 0x00 }, job.TakeLast(3).ToArray());
    }

    private static bool ContainsSequence(byte[] source, byte[] value)
    {
        for (var index = 0; index <= source.Length - value.Length; index++)
        {
            if (source.AsSpan(index, value.Length).SequenceEqual(value))
                return true;
        }

        return false;
    }
}
