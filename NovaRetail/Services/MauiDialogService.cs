namespace NovaRetail.Services;

public sealed class MauiDialogService : IDialogService
{
    private static Page CurrentPage => Application.Current!.Windows[0].Page!;

    public Task AlertAsync(string title, string message, string cancel) =>
        CurrentPage.DisplayAlertAsync(title, message, cancel);

    public Task<bool> ConfirmAsync(string title, string message, string accept, string cancel) =>
        CurrentPage.DisplayAlertAsync(title, message, accept, cancel);

    public Task<string?> PromptAsync(string title, string message, string accept = "OK", string cancel = "Cancel",
        string? placeholder = null, int maxLength = -1, Keyboard? keyboard = null, string initialValue = "") =>
        CurrentPage.DisplayPromptAsync(title, message, accept, cancel,
            placeholder, maxLength, keyboard ?? Keyboard.Default, initialValue);
}
