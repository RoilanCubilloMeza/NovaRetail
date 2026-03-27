using System.Text.RegularExpressions;

namespace NovaRetail.Services;

/// <summary>
/// Utilidad estática para validar identificaciones de Costa Rica.
/// Centraliza reglas de entrada por tipo de documento, validación final y expresiones regulares
/// usadas por la pantalla de cliente para evitar caracteres o formatos inválidos.
/// </summary>
public static partial class IdValidatorCR
{
    [GeneratedRegex("^[0-9]$")]
    private static partial Regex NumericCharRegex();

    [GeneratedRegex("^[A-Za-z0-9]$")]
    private static partial Regex AlphanumericCharRegex();

    [GeneratedRegex(@"^\d+$")]
    private static partial Regex DigitsOnlyRegex();

    [GeneratedRegex(@"^[A-Za-z0-9]+$")]
    private static partial Regex AlphanumericOnlyRegex();

    [GeneratedRegex(@"^[A-Za-z0-9]{1,20}$")]
    private static partial Regex ForeignIdRegex();

    public static bool IsInputCharAllowed(string tipoCodigo, string inputChar)
    {
        if (string.IsNullOrEmpty(inputChar)) return false;

        var isNumeric = NumericCharRegex().IsMatch(inputChar);
        var isAlnum = AlphanumericCharRegex().IsMatch(inputChar);

        return tipoCodigo switch
        {
            "01" or "02" or "03" or "04" or "06" => isNumeric,
            "05" => isAlnum,
            _ => false
        };
    }

    public static bool IsPasteAllowed(string tipoCodigo, string pastedText)
    {
        if (string.IsNullOrWhiteSpace(pastedText)) return false;

        return tipoCodigo switch
        {
            "01" or "02" or "03" or "04" or "06" => DigitsOnlyRegex().IsMatch(pastedText),
            "05" => AlphanumericOnlyRegex().IsMatch(pastedText),
            _ => false
        };
    }

    public static bool ValidateFinal(string tipoCodigo, string id, out string? error)
    {
        error = null;

        if (string.IsNullOrWhiteSpace(tipoCodigo))
        {
            error = "Debe seleccionar el tipo de identificación.";
            return false;
        }

        id = (id ?? string.Empty).Trim();

        if (tipoCodigo is "01" or "02" or "03" or "04" or "06")
        {
            if (!DigitsOnlyRegex().IsMatch(id))
            {
                error = "El número debe contener solo dígitos.";
                return false;
            }
        }
        else if (tipoCodigo == "05")
        {
            if (!ForeignIdRegex().IsMatch(id))
            {
                error = "Extranjero no domiciliado: hasta 20 caracteres alfanuméricos (sin espacios ni símbolos).";
                return false;
            }
        }

        switch (tipoCodigo)
        {
            case "01":
                if (id.Length == 9 && !id.StartsWith("0")) return true;
                error = "Cédula física: 9 dígitos, sin cero inicial ni guiones.";
                return false;

            case "02":
                if (id.Length == 10) return true;
                error = "Cédula jurídica: 10 dígitos, sin guiones.";
                return false;

            case "03":
                if ((id.Length == 11 || id.Length == 12) && !id.StartsWith("0")) return true;
                error = "DIMEX: 11 o 12 dígitos, sin cero inicial ni guiones.";
                return false;

            case "04":
                if (id.Length == 10) return true;
                error = "NITE: 10 dígitos, sin guiones.";
                return false;

            case "05":
                return true;

            case "06":
                if (id.Length >= 1 && id.Length <= 20) return true;
                error = "No Contribuyente: entre 1 y 20 dígitos.";
                return false;
        }

        error = "Tipo de identificación no válido.";
        return false;
    }
}
