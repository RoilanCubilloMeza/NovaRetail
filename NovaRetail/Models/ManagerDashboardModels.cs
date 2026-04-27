namespace NovaRetail.Models;

public sealed class ManagerDashboardResponse
{
    public bool Ok { get; set; }
    public string Message { get; set; } = string.Empty;
    public DateTime BusinessDate { get; set; }
    public int SalesTodayCount { get; set; }
    public decimal SalesTodayTotal { get; set; }
    public int QuotesCreatedToday { get; set; }
    public int QuotesConvertedToday { get; set; }
    public int PendingWorkOrders { get; set; }
    public int PaymentsReceivedTodayCount { get; set; }
    public decimal PaymentsReceivedTodayTotal { get; set; }
}

public sealed class ManagerActionLogResponse
{
    public bool Ok { get; set; }
    public string Message { get; set; } = string.Empty;
    public List<ManagerActionLogEntry> Actions { get; set; } = new();
}

public sealed class ManagerActionLogEntry
{
    public int ID { get; set; }
    public DateTime ActionDate { get; set; }
    public string ActionType { get; set; } = string.Empty;
    public string EntityType { get; set; } = string.Empty;
    public int EntityID { get; set; }
    public int CashierID { get; set; }
    public string CashierName { get; set; } = string.Empty;
    public int StoreID { get; set; }
    public int RegisterID { get; set; }
    public decimal Amount { get; set; }
    public string Detail { get; set; } = string.Empty;

    public string ActionDateText => ActionDate.ToString("dd/MM/yyyy HH:mm");
    public string SellerName => !string.IsNullOrWhiteSpace(CashierName) ? CashierName : CashierID > 0 ? $"Usuario {CashierID}" : "No registrado";
    public string SellerText => $"Vendedor: {SellerName}";
    public string AmountText => Amount == 0m ? string.Empty : $"{UiConfig.CurrencySymbol}{Amount:N2}";
    public string EntityText => EntityID > 0 ? $"{EntityDisplayName} #{EntityID}" : EntityDisplayName;
    public string ActionDisplayName => ActionType switch
    {
        "PriceChanged" => "Cambio de precio",
        "DiscountApplied" => "Descuento aplicado",
        "OrderModified" => "Orden modificada",
        "OrderCanceled" => "Cancelacion",
        "SaleCreated" => "Venta realizada",
        "QuoteCreated" => "Cotizacion creada",
        "QuoteConverted" => "Cotizacion convertida",
        "WorkOrderCreated" => "Orden creada",
        "HoldCreated" => "Espera creada",
        "PaymentReceived" => "Abono recibido",
        _ => ActionType
    };
    public string EntityDisplayName => EntityType switch
    {
        "Quote" => "Cotizacion",
        "WorkOrder" => "Orden trabajo",
        "Hold" => "Factura espera",
        "Sale" => "Venta",
        "Payment" => "Abono",
        _ => EntityType
    };
}
