namespace NovaRetail.Services;

public sealed class MauiDialogService : IDialogService
{
    public Task AlertAsync(string title, string message, string cancel) =>
        UiPage.AlertAsync(title, message, cancel);

    public Task<bool> ConfirmAsync(string title, string message, string accept, string cancel) =>
        MainThread.InvokeOnMainThreadAsync(() =>
            UiPage.Current?.DisplayAlertAsync(title, message, accept, cancel) ?? Task.FromResult(false));

    public Task<string?> PromptAsync(string title, string message, string accept = "OK", string cancel = "Cancel",
        string? placeholder = null, int maxLength = -1, Keyboard? keyboard = null, string initialValue = "") =>
        MainThread.InvokeOnMainThreadAsync(() =>
            UiPage.Current?.DisplayPromptAsync(title, message, accept, cancel,
                placeholder, maxLength, keyboard ?? Keyboard.Default, initialValue)
            ?? Task.FromResult<string?>(null));
}
