namespace NovaRetail.Services;

/// <summary>
/// Utilidades compartidas para normalizar códigos de actividad económica (Costa Rica).
/// </summary>
public static class ActivityCodeHelper
{
    /// <summary>
    /// Normaliza un código de actividad económica a 6 dígitos con padding de ceros a la izquierda.
    /// Retorna <c>null</c> si el código es inválido o vacío.
    /// </summary>
    public static string? Normalize(string? code)
    {
        if (string.IsNullOrWhiteSpace(code))
            return null;

        var digits = new string(code.Where(char.IsDigit).ToArray());
        if (digits.Length == 0)
            return null;

        if (digits.Length > 6)
            return digits[..6];

        return digits.PadLeft(6, '0');
    }
}
