namespace NovaRetail.Models
{
    public class ReasonCodeModel
    {
        public int ID { get; set; }
        public int Type { get; set; }
        public string Code { get; set; } = string.Empty;
        public string Description { get; set; } = string.Empty;

        public string DisplayText => string.IsNullOrWhiteSpace(Description) ? Code : $"{Code} – {Description}";

        public string TypeName => Type switch
        {
            3 => "Override de Precio",
            4 => "Descuento del cliente",
            5 => "Nota de Crédito",
            6 => "Exoneración",
            _ => $"Tipo {Type}"
        };
    }
}
