namespace NovaRetail.Models;

public sealed class InvoiceHistoryEntry
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public DateTime Date { get; set; } = DateTime.Now;
    public bool IsLocalEntry { get; set; } = true;

    // Transacción
    public int TransactionNumber { get; set; }
    public string ComprobanteTipo { get; set; } = "04";
    public string Clave50 { get; set; } = string.Empty;
    public string Consecutivo { get; set; } = string.Empty;

    // Cliente
    public string ClientId { get; set; } = string.Empty;
    public string ClientName { get; set; } = string.Empty;

    // Cajero / Tienda
    public string CashierName { get; set; } = string.Empty;
    public int RegisterNumber { get; set; } = 1;
    public string StoreName { get; set; } = string.Empty;

    // Totales
    public decimal SubtotalColones { get; set; }
    public decimal DiscountColones { get; set; }
    public decimal ExonerationColones { get; set; }
    public decimal TaxColones { get; set; }
    public decimal TotalColones { get; set; }
    public decimal ChangeColones { get; set; }

    // Pago
    public string TenderDescription { get; set; } = string.Empty;
    public decimal TenderTotalColones { get; set; }
    public string SecondTenderDescription { get; set; } = string.Empty;
    public decimal SecondTenderAmountColones { get; set; }

    // Líneas
    public List<InvoiceHistoryLine> Lines { get; set; } = new();

    // ── Propiedades de presentación ──
    public string DocumentTypeName => ComprobanteTipo switch
    {
        "01" => "Factura Electrónica",
        "03" => "Nota de Crédito",
        "04" => "Tiquete Electrónico",
        "10" => "Reposición",
        _    => "Tiquete Electrónico"
    };

    public string TotalColonesText  => $"{UiConfig.CurrencySymbol}{TotalColones:N2}";
    public string SubtotalColonesText => $"{UiConfig.CurrencySymbol}{SubtotalColones:N2}";
    public string DiscountColonesNegativeText => $"-{UiConfig.CurrencySymbol}{DiscountColones:N2}";
    public string ExonerationColonesNegativeText => $"-{UiConfig.CurrencySymbol}{ExonerationColones:N2}";
    public string TaxColonesText => $"{UiConfig.CurrencySymbol}{TaxColones:N2}";
    public string ChangeColonesText => $"{UiConfig.CurrencySymbol}{ChangeColones:N2}";
    public string TenderTotalColonesText => $"{UiConfig.CurrencySymbol}{TenderTotalColones:N2}";
    public string SecondTenderAmountText => $"{UiConfig.CurrencySymbol}{SecondTenderAmountColones:N2}";
    public string DateText          => Date.ToString("dd/MM/yyyy HH:mm");
    public string TransactionText   => $"#{TransactionNumber}";
    public string ClientMetaText    => string.IsNullOrWhiteSpace(ClientId) ? DateText : $"{ClientId} · {DateText}";
    public string DocumentIcon      => ComprobanteTipo switch
    {
        "01" => "📄",
        "03" => "↩",
        "04" => "🧾",
        "10" => "🔁",
        _    => "🧾"
    };
    public string SourceLabel => IsLocalEntry ? "Local" : "Servidor";
    public string SourceBadgeBackground => IsLocalEntry ? "#DCFCE7" : "#DBEAFE";
    public string SourceBadgeTextColor => IsLocalEntry ? "#166534" : "#1D4ED8";
    public string TenderDeliveredText => $"{(string.IsNullOrWhiteSpace(TenderDescription) ? "Pago" : TenderDescription)} entregado";
    public string SecondTenderText => $"2do pago: {SecondTenderDescription}";
    public bool CanDelete           => IsLocalEntry;
    public bool HasFiscalData       => !string.IsNullOrWhiteSpace(Clave50);
    public bool HasDiscount         => DiscountColones > 0;
    public bool HasExoneration      => ExonerationColones > 0;
    public bool HasChange           => ChangeColones > 0;
    public bool HasSecondTender     => SecondTenderAmountColones > 0 && !string.IsNullOrWhiteSpace(SecondTenderDescription);
}

public sealed class InvoiceHistoryLine
{
    public int ItemID { get; set; }
    public int TaxID { get; set; }
    public string DisplayName { get; set; } = string.Empty;
    public string Code { get; set; } = string.Empty;
    public decimal Quantity { get; set; }
    public decimal TaxPercentage { get; set; }
    public decimal UnitPriceColones { get; set; }
    public decimal LineTotalColones { get; set; }
    public bool HasDiscount { get; set; }
    public decimal DiscountPercent { get; set; }
    public bool HasExoneration { get; set; }
    public decimal ExonerationPercent { get; set; }
    public bool HasOverridePrice { get; set; }
    public bool HasTax => TaxPercentage > 0;
    public string QuantityText => Quantity.ToString("0.##");
    public string TaxPercentageText => $"{TaxPercentage:0.##} %";
    public string UnitPriceText => $"{UiConfig.CurrencySymbol}{UnitPriceColones:N2}";
    public string LineTotalText => $"{UiConfig.CurrencySymbol}{LineTotalColones:N2}";
    public string DiscountText => $"Desc. {DiscountPercent:0.##}%";
    public string ExonerationText => $"Exoneración {ExonerationPercent:0.##}%";
}
