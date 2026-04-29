using System.Text;
#if WINDOWS
using Microsoft.Maui.Platform;
using WinRT.Interop;
#endif

namespace NovaRetail.Services;

internal static class DocumentSaveService
{
    public static async Task SaveHtmlAsync(string title, string suggestedFileName, string htmlContent)
    {
#if WINDOWS
        var normalizedFileName = NormalizeHtmlFileName(suggestedFileName);
        var picker = new Windows.Storage.Pickers.FileSavePicker
        {
            SuggestedStartLocation = Windows.Storage.Pickers.PickerLocationId.DocumentsLibrary,
            SuggestedFileName = Path.GetFileNameWithoutExtension(normalizedFileName)
        };

        picker.FileTypeChoices.Add("Documento HTML", new List<string> { ".html" });

        if (Application.Current?.Windows.FirstOrDefault()?.Handler?.PlatformView is not MauiWinUIWindow nativeWindow)
            throw new InvalidOperationException("No se pudo obtener la ventana activa para guardar el documento.");

        InitializeWithWindow.Initialize(picker, WindowNative.GetWindowHandle(nativeWindow));

        var file = await picker.PickSaveFileAsync();
        if (file is null)
            return;

        await Windows.Storage.FileIO.WriteTextAsync(file, htmlContent, Windows.Storage.Streams.UnicodeEncoding.Utf8);
#else
        var path = Path.Combine(FileSystem.CacheDirectory, NormalizeHtmlFileName(suggestedFileName));
        File.WriteAllText(path, htmlContent, Encoding.UTF8);
        await Share.RequestAsync(new ShareFileRequest
        {
            Title = title,
            File = new ShareFile(path, "text/html")
        });
#endif
    }

    private static string NormalizeHtmlFileName(string suggestedFileName)
    {
        if (string.IsNullOrWhiteSpace(suggestedFileName))
            return "documento.html";

        return suggestedFileName.EndsWith(".html", StringComparison.OrdinalIgnoreCase)
            ? suggestedFileName
            : $"{suggestedFileName}.html";
    }
}