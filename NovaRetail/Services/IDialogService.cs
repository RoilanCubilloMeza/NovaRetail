namespace NovaRetail.Services;

/// <summary>Contrato de abstracción para diálogos de interfaz de usuario.</summary>
public interface IDialogService
{
    Task AlertAsync(string title, string message, string cancel);
    Task<bool> ConfirmAsync(string title, string message, string accept, string cancel);
    Task<string?> PromptAsync(string title, string message, string accept = "OK", string cancel = "Cancel",
        string? placeholder = null, int maxLength = -1, Keyboard? keyboard = null, string initialValue = "");
}
