namespace NovaRetail.Models
{
    public class StoreConfigModel
    {
        public int StoreID { get; set; }
        /// <summary>0 = IVA Excluido, 1 = IVA Incluido</summary>
        public int TaxSystem { get; set; }
        public int QuoteExpirationDays { get; set; }
        public int DefaultTenderID { get; set; }

        public string TaxSystemText => TaxSystem == 1 ? "IVA Incluido" : "IVA Excluido";
    }

    public class TenderModel
    {
        public int ID { get; set; }
        public string Description { get; set; } = string.Empty;
        public int CurrencyID { get; set; }
        public int DisplayOrder { get; set; }

        /// <summary>Símbolo de moneda para mostrar en UI</summary>
        public string CurrencySymbol => CurrencyID switch
        {
            1 => "₡",
            2 => "$",
            _ => "#"
        };

        public string DisplayText => $"{Description}  ({CurrencySymbol})";
    }
}
