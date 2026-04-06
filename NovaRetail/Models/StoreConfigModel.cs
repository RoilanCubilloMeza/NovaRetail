namespace NovaRetail.Models;

public class StoreConfigModel
{
    public int StoreID { get; set; }
    public int RegisterID { get; set; }
    public int BatchNumber { get; set; }
    /// <summary>0 = IVA Excluido, valores mayores a 0 = IVA Incluido (compatibilidad RMH).</summary>
    public int TaxSystem { get; set; }
    public int QuoteExpirationDays { get; set; }
    public int DefaultTenderID { get; set; }
    public string StoreName { get; set; } = string.Empty;
    public string StoreAddress { get; set; } = string.Empty;
    public string StorePhone { get; set; } = string.Empty;

    public bool IsTaxIncluded => TaxSystem > 0;

    public string TaxSystemText => IsTaxIncluded ? "IVA Incluido" : "IVA Excluido";

    /// <summary>PriceSource a usar cuando el precio se sobreescribe hacia arriba (valor de PR-01 en AVS_Parametros).</summary>
    public int PriceOverridePriceSource { get; set; } = 1;

    /// <summary>VE-01: Si se debe pedir vendedor al iniciar sesión.</summary>
    public bool AskForSalesRep { get; set; }

    /// <summary>VE-02: Si el vendedor es obligatorio para facturar.</summary>
    public bool RequireSalesRep { get; set; }

    /// <summary>Porcentaje de impuesto por defecto (primer registro de Tax en la DB).</summary>
    public decimal DefaultTaxPercentage { get; set; } = 13m;

    /// <summary>Tipo de cambio por defecto (TC-01 en AVS_Parametros).</summary>
    public decimal DefaultExchangeRate { get; set; }

    /// <summary>Código del cliente contado por defecto (CL-01 en AVS_Parametros).</summary>
    public string DefaultClientId { get; set; } = "00001";

    /// <summary>Nombre del cliente contado por defecto (CL-02 en AVS_Parametros).</summary>
    public string DefaultClientName { get; set; } = "CLIENTE CONTADO";

    /// <summary>IT-01: IDs de ItemType no inventariables separados por coma (ej: "7,5,9").</summary>
    public string NonInventoryItemTypes { get; set; } = string.Empty;
}

public class TenderModel : System.ComponentModel.INotifyPropertyChanged
{
    private bool _isSelected;
    private bool _isSecondSelected;

    public int ID { get; set; }
    public string Description { get; set; } = string.Empty;
    public int CurrencyID { get; set; }
    public int DisplayOrder { get; set; }

    /// <summary>Código de medio de pago para facturación electrónica (01=Efectivo, 02=Tarjeta, 04=Transferencia, etc.).</summary>
    public string MedioPagoCodigo { get; set; } = string.Empty;

    /// <summary>Símbolo de moneda para mostrar en UI</summary>
    public string CurrencySymbol => CurrencyID switch
    {
        1 => UiConfig.CurrencySymbol,
        2 => "$",
        _ => "#"
    };

    public string DisplayText => $"{Description}  ({CurrencySymbol})";

    /// <summary>True si este medio de pago es de tipo crédito (cuenta corriente del cliente).</summary>
    public bool IsCredit => (Description ?? string.Empty).Contains("crédito", StringComparison.OrdinalIgnoreCase)
        || (Description ?? string.Empty).Contains("credito", StringComparison.OrdinalIgnoreCase);

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
