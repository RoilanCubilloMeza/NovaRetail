namespace NovaRetail.Models
{
    public class StoreConfigModel
    {
        public int StoreID { get; set; }
        public int RegisterID { get; set; }
        public int BatchNumber { get; set; }
        /// <summary>0 = IVA Excluido, 1 = IVA Incluido</summary>
        public int TaxSystem { get; set; }
        public int QuoteExpirationDays { get; set; }
        public int DefaultTenderID { get; set; }
        public string StoreName { get; set; } = string.Empty;
        public string StoreAddress { get; set; } = string.Empty;
        public string StorePhone { get; set; } = string.Empty;

        public string TaxSystemText => TaxSystem == 1 ? "IVA Incluido" : "IVA Excluido";

        /// <summary>PriceSource a usar cuando el precio se sobreescribe hacia arriba (valor de PR-01 en AVS_Parametros).</summary>
        public int PriceOverridePriceSource { get; set; } = 1;
    }

    public class TenderModel : System.ComponentModel.INotifyPropertyChanged
    {
        private bool _isSelected;
        private bool _isSecondSelected;

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

        public bool IsSelected
        {
            get => _isSelected;
            set { if (_isSelected != value) { _isSelected = value; PropertyChanged?.Invoke(this, new System.ComponentModel.PropertyChangedEventArgs(nameof(IsSelected))); } }
        }

        public bool IsSecondSelected
        {
            get => _isSecondSelected;
            set { if (_isSecondSelected != value) { _isSecondSelected = value; PropertyChanged?.Invoke(this, new System.ComponentModel.PropertyChangedEventArgs(nameof(IsSecondSelected))); } }
        }

        public event System.ComponentModel.PropertyChangedEventHandler? PropertyChanged;
    }
}
