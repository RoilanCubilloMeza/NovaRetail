namespace NovaRetail.Services;

internal static class UiPage
{
    public static Page? Current => Application.Current?.Windows.FirstOrDefault()?.Page;

    public static Task AlertAsync(string title, string message, string cancel = "OK") =>
        MainThread.InvokeOnMainThreadAsync(() =>
            Current?.DisplayAlertAsync(title, message, cancel) ?? Task.CompletedTask);
}