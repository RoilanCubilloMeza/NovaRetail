using System.ComponentModel;
using System.Runtime.InteropServices;
using System.Text;

namespace NovaRetail.Services;

public static class RawPrinterHelper
{
    [StructLayout(LayoutKind.Sequential, CharSet = CharSet.Unicode)]
    private sealed class DocInfo
    {
        public string pDocName = "Ticket";
        public string? pOutputFile;
        public string pDataType = "RAW";
    }

    [DllImport("winspool.drv", SetLastError = true, CharSet = CharSet.Unicode)]
    private static extern bool OpenPrinter(string printerName, out IntPtr printerHandle, IntPtr defaults);

    [DllImport("winspool.drv", SetLastError = true)]
    private static extern bool ClosePrinter(IntPtr printerHandle);

    [DllImport("winspool.drv", SetLastError = true, CharSet = CharSet.Unicode)]
    private static extern bool StartDocPrinter(IntPtr printerHandle, int level, [In] DocInfo docInfo);

    [DllImport("winspool.drv", SetLastError = true)]
    private static extern bool EndDocPrinter(IntPtr printerHandle);

    [DllImport("winspool.drv", SetLastError = true)]
    private static extern bool StartPagePrinter(IntPtr printerHandle);

    [DllImport("winspool.drv", SetLastError = true)]
    private static extern bool EndPagePrinter(IntPtr printerHandle);

    [DllImport("winspool.drv", SetLastError = true)]
    private static extern bool WritePrinter(IntPtr printerHandle, byte[] bytes, int count, out int written);

    [DllImport("winspool.drv", SetLastError = true, CharSet = CharSet.Unicode)]
    private static extern bool GetDefaultPrinter(StringBuilder? printerName, ref int size);

    public static string GetDefaultPrinterName()
    {
        var size = 0;
        GetDefaultPrinter(null, ref size);
        if (size <= 0)
            throw new InvalidOperationException("Windows no tiene una impresora predeterminada configurada.");

        var printerName = new StringBuilder(size);
        if (!GetDefaultPrinter(printerName, ref size))
            throw new Win32Exception(Marshal.GetLastWin32Error(), "No se pudo obtener la impresora predeterminada.");

        return printerName.ToString();
    }

    public static void SendBytesToPrinter(string printerName, byte[] bytes)
    {
        if (!OpenPrinter(printerName, out var printerHandle, IntPtr.Zero))
            throw new Win32Exception(Marshal.GetLastWin32Error(), $"No se pudo abrir la impresora '{printerName}'.");

        try
        {
            if (!StartDocPrinter(printerHandle, 1, new DocInfo()))
                throw new Win32Exception(Marshal.GetLastWin32Error(), "No se pudo iniciar el documento RAW.");

            try
            {
                if (!StartPagePrinter(printerHandle))
                    throw new Win32Exception(Marshal.GetLastWin32Error(), "No se pudo iniciar la pagina de impresion.");

                try
                {
                    if (!WritePrinter(printerHandle, bytes, bytes.Length, out var written) || written != bytes.Length)
                        throw new Win32Exception(Marshal.GetLastWin32Error(), "No se pudo enviar el comprobante completo a la impresora.");
                }
                finally
                {
                    EndPagePrinter(printerHandle);
                }
            }
            finally
            {
                EndDocPrinter(printerHandle);
            }
        }
        finally
        {
            ClosePrinter(printerHandle);
        }
    }
}
