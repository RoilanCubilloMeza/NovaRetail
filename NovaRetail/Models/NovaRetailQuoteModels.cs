namespace NovaRetail.Models;

public sealed class NovaRetailCreateQuoteRequest
{
    public int OrderID { get; set; }
    public int StoreID { get; set; }
    /// <summary>1 = Factura en Espera, 2 = Orden de Trabajo, 3 = Cotización (default).</summary>
    public int Type { get; set; } = 3;
    public int CustomerID { get; set; }
    public int ShipToID { get; set; }
    public int CashierID { get; set; }
    public int RegisterID { get; set; }
    public string Comment { get; set; } = string.Empty;
    public string ReferenceNumber { get; set; } = string.Empty;
    public int SalesRepID { get; set; }
    public bool Taxable { get; set; } = true;
    public int ExchangeID { get; set; }
    public int ChannelType { get; set; }
    public int DefaultDiscountReasonCodeID { get; set; }
    public int DefaultReturnReasonCodeID { get; set; }
    public int DefaultTaxChangeReasonCodeID { get; set; }
    public DateTime? ExpirationOrDueDate { get; set; }
    public decimal Tax { get; set; }
    public decimal Total { get; set; }
    public bool AllowNegativeInventory { get; set; }
    public List<NovaRetailQuoteItemRequest> Items { get; set; } = new();
}

public sealed class NovaRetailQuoteItemRequest
{
    public int ItemID { get; set; }
    public decimal Cost { get; set; }
    public decimal FullPrice { get; set; }
    public int PriceSource { get; set; } = 1;
    public decimal Price { get; set; }
    public decimal QuantityOnOrder { get; set; } = 1;
    public int SalesRepID { get; set; }
    public bool Taxable { get; set; } = true;
    public int DetailID { get; set; }
    public string Description { get; set; } = string.Empty;
    public string Comment { get; set; } = string.Empty;
    public int DiscountReasonCodeID { get; set; }
    public int ReturnReasonCodeID { get; set; }
    public int TaxChangeReasonCodeID { get; set; }
}

public sealed class NovaRetailCreateQuoteResponse
{
    public bool Ok { get; set; }
    public string Message { get; set; } = string.Empty;
    public int OrderID { get; set; }
    public decimal Tax { get; set; }
    public decimal Total { get; set; }
    public List<string> Warnings { get; set; } = new();
}

public sealed class NovaRetailOrderSummary
{
    public int OrderID { get; set; }
    public int Type { get; set; }
    public string Comment { get; set; } = string.Empty;
    public decimal Total { get; set; }
    public decimal Tax { get; set; }
    public DateTime Time { get; set; }
    public DateTime? ExpirationOrDueDate { get; set; }
    public int CustomerID { get; set; }
    public int ItemCount { get; set; }
    public string ReferenceNumber { get; set; } = string.Empty;
    public string CashierName { get; set; } = string.Empty;

    public string DisplayTitle
    {
        get
        {
            var id = $"#{OrderID}";
            var label = !string.IsNullOrWhiteSpace(Comment) ? Comment.Trim()
                : !string.IsNullOrWhiteSpace(ReferenceNumber) ? ParseClientName()
                : null;
            return label != null ? $"{id} — {label}" : $"{TypeDisplayName} {id}";
        }
    }
    public string DisplayDate => Time.ToString("dd/MM/yyyy HH:mm");
    public string DisplayTotal => $"{UiConfig.CurrencySymbol}{Total:N2}";
    public string DisplayItems => $"{ItemCount} art.";
    public string DisplayCashier => string.IsNullOrWhiteSpace(CashierName) ? "—" : CashierName;
    public string DisplayClient => ParseClientName();
    public bool HasClientRef => !string.IsNullOrWhiteSpace(ReferenceNumber);
    public string TypeDisplayName => Type switch
    {
        1 => "Factura en espera",
        2 => "Orden de trabajo",
        3 => "Cotización",
        _ => "Orden"
    };
    public bool CanCancel => Type == 2 || Type == 3;
    public string CancelActionText => Type == 2 ? "Cancelar orden de trabajo" : "Cancelar cotización";

    /// <summary>Extrae la cédula del formato "cédula|nombre" almacenado en ReferenceNumber.</summary>
    public string ParseClientId()
    {
        if (string.IsNullOrWhiteSpace(ReferenceNumber)) return string.Empty;
        var idx = ReferenceNumber.IndexOf('|');
        return idx > 0 ? ReferenceNumber[..idx].Trim() : string.Empty;
    }

    /// <summary>Extrae el nombre del formato "cédula|nombre" almacenado en ReferenceNumber.</summary>
    public string ParseClientName()
    {
        if (string.IsNullOrWhiteSpace(ReferenceNumber)) return string.Empty;
        var idx = ReferenceNumber.IndexOf('|');
        return idx >= 0 ? ReferenceNumber[(idx + 1)..].Trim() : ReferenceNumber.Trim();
    }
}

public sealed class NovaRetailOrderDetail
{
    public int OrderID { get; set; }
    public int Type { get; set; }
    public string Comment { get; set; } = string.Empty;
    public decimal Total { get; set; }
    public decimal Tax { get; set; }
    public DateTime Time { get; set; }
    public List<NovaRetailOrderEntry> Entries { get; set; } = new();
}

public sealed class NovaRetailOrderEntry
{
    public int EntryID { get; set; }
    public int ItemID { get; set; }
    public string Description { get; set; } = string.Empty;
    public decimal Price { get; set; }
    public decimal FullPrice { get; set; }
    public decimal Cost { get; set; }
    public decimal QuantityOnOrder { get; set; }
    public int SalesRepID { get; set; }
    public bool Taxable { get; set; }
    public int TaxID { get; set; }
    public int ItemType { get; set; }
    public int PriceSource { get; set; }
    public int DetailID { get; set; }
    public string Comment { get; set; } = string.Empty;
    public int DiscountReasonCodeID { get; set; }
    public int ReturnReasonCodeID { get; set; }
    public int TaxChangeReasonCodeID { get; set; }
}

public sealed class NovaRetailListOrdersResponse
{
    public bool Ok { get; set; }
    public string Message { get; set; } = string.Empty;
    public List<NovaRetailOrderSummary> Orders { get; set; } = new();
}

public sealed class NovaRetailOrderDetailResponse
{
    public bool Ok { get; set; }
    public string Message { get; set; } = string.Empty;
    public NovaRetailOrderDetail? Order { get; set; }
}
