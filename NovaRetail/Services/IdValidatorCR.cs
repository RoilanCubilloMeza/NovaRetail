using System.Text.RegularExpressions;

namespace NovaRetail.Services
{
    public static class IdValidatorCR
    {
        public static bool IsInputCharAllowed(string tipoCodigo, string inputChar)
        {
            if (string.IsNullOrEmpty(inputChar)) return false;

            var isNumeric = Regex.IsMatch(inputChar, "^[0-9]$");
            var isAlnum = Regex.IsMatch(inputChar, "^[A-Za-z0-9]$");

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
                "01" or "02" or "03" or "04" or "06" => Regex.IsMatch(pastedText, @"^\d+$"),
                "05" => Regex.IsMatch(pastedText, @"^[A-Za-z0-9]+$"),
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
                if (!Regex.IsMatch(id, @"^\d+$"))
                {
                    error = "El número debe contener solo dígitos.";
                    return false;
                }
            }
            else if (tipoCodigo == "05")
            {
                if (!Regex.IsMatch(id, @"^[A-Za-z0-9]{1,20}$"))
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
}
