namespace NovaRetail.Models;

public sealed class InvoiceHistoryEntry
{
    private string _comprobanteTipo = "04";
    private List<InvoiceHistoryLine> _lines = new();

    public Guid Id { get; set; } = Guid.NewGuid();
    public DateTime Date { get; set; } = DateTime.Now;
    public bool IsLocalEntry { get; set; } = true;

    public int TransactionNumber { get; set; }

    public string ComprobanteTipo
    {
        get => _comprobanteTipo;
        set
        {
            _comprobanteTipo = string.IsNullOrWhiteSpace(value) ? "04" : value;
            SyncLinePresentation();
        }
    }

    public string Clave50 { get; set; } = string.Empty;
    public string Consecutivo { get; set; } = string.Empty;

    public string ClientId { get; set; } = string.Empty;
    public string ClientName { get; set; } = string.Empty;

    public string CashierName { get; set; } = string.Empty;
    public int RegisterNumber { get; set; } = 1;
    public string StoreName { get; set; } = string.Empty;

    public decimal SubtotalColones { get; set; }
    public decimal DiscountColones { get; set; }
    public decimal ExonerationColones { get; set; }
    public decimal TaxColones { get; set; }
    public decimal TotalColones { get; set; }
    public decimal ChangeColones { get; set; }

    public string CreditAccountNumber { get; set; } = string.Empty;
    public int SourceTransactionNumber { get; set; }
    public int ClosedByCreditNoteTransactionNumber { get; set; }
    public int LastAppliedCreditNoteTransactionNumber { get; set; }
    public int AppliedSourceTransactionNumber { get; set; }
    public decimal CreditedAmountColones { get; set; }

    public string TenderDescription { get; set; } = string.Empty;
    public decimal TenderTotalColones { get; set; }
    public string SecondTenderDescription { get; set; } = string.Empty;
    public decimal SecondTenderAmountColones { get; set; }

    public List<InvoiceHistoryLine> Lines
    {
        get => _lines;
        set
        {
            _lines = value ?? new List<InvoiceHistoryLine>();
            SyncLinePresentation();
        }
    }

    public bool IsCreditNote => ComprobanteTipo == "03";
    public bool HasRelatedSourceTransaction => SourceTransactionNumber > 0;
    public bool HasAppliedSourceTransaction => AppliedSourceTransactionNumber > 0;
    public bool IsReturnCompleted => !IsCreditNote && CreditedAmountColones > 0m && CreditedAmountColones + 0.01m >= Math.Abs(TotalColones);
    public bool IsReturnPartial => !IsCreditNote && CreditedAmountColones > 0m && !IsReturnCompleted;
    public bool HasLifecycleBadge => (IsCreditNote && HasRelatedSourceTransaction) || IsReturnCompleted || IsReturnPartial;

    public string DocumentTypeName => ComprobanteTipo switch
    {
        "01" => "Factura Electronica",
        "03" => "Nota de Credito",
        "04" => "Tiquete Electronico",
        "10" => "Reposicion",
        _ => "Tiquete Electronico"
    };

    public string TotalColonesText => FormatSignedCurrency(TotalColones);
    public string SubtotalColonesText => FormatSignedCurrency(SubtotalColones);
    public string DiscountColonesNegativeText => $"-{UiConfig.CurrencySymbol}{DiscountColones:N2}";
    public string ExonerationColonesNegativeText => $"-{UiConfig.CurrencySymbol}{ExonerationColones:N2}";
    public string TaxColonesText => FormatSignedCurrency(TaxColones);
    public string ChangeColonesText => $"{UiConfig.CurrencySymbol}{ChangeColones:N2}";
    public string TenderTotalColonesText => FormatSignedCurrency(TenderTotalColones > 0 ? TenderTotalColones : TotalColones);
    public string SecondTenderAmountText => $"{UiConfig.CurrencySymbol}{SecondTenderAmountColones:N2}";
    public string DateText => Date.ToString("dd/MM/yyyy HH:mm");
    public string TransactionText => $"#{TransactionNumber}";
    public string ClientMetaText => string.IsNullOrWhiteSpace(ClientId) ? DateText : $"{ClientId} - {DateText}";
    public string DocumentIcon => ComprobanteTipo switch
    {
        "01" => "FE",
        "03" => "NC",
        "04" => "TE",
        "10" => "RP",
        _ => "TE"
    };
    public string SourceLabel => IsLocalEntry ? "Local" : "Servidor";
    public string SourceBadgeBackground => IsLocalEntry ? "#DCFCE7" : "#DBEAFE";
    public string SourceBadgeTextColor => IsLocalEntry ? "#166534" : "#1D4ED8";
    public string LifecycleBadgeText => IsCreditNote
        ? HasAppliedSourceTransaction
            ? $"Aplicada a #{AppliedSourceTransactionNumber}"
            : HasRelatedSourceTransaction
                ? $"Referenciada a #{SourceTransactionNumber}"
                : string.Empty
        : IsReturnCompleted
            ? $"Devolucion completa NC #{LastAppliedCreditNoteTransactionNumber}"
        : IsReturnPartial
            ? $"Devolucion parcial NC #{LastAppliedCreditNoteTransactionNumber}"
            : string.Empty;
    public string LifecycleBadgeBackground => IsCreditNote
        ? HasAppliedSourceTransaction
            ? "#DBEAFE"
            : "#EDE9FE"
        : IsReturnCompleted
            ? "#FEF3C7"
        : IsReturnPartial
            ? "#E0F2FE"
            : "#DBEAFE";
    public string LifecycleBadgeTextColor => IsCreditNote
        ? HasAppliedSourceTransaction
            ? "#1D4ED8"
            : "#6D28D9"
        : IsReturnCompleted
            ? "#92400E"
        : IsReturnPartial
            ? "#075985"
            : "#1D4ED8";
    public string TenderDeliveredText => $"{(string.IsNullOrWhiteSpace(TenderDescription) ? "Pago" : TenderDescription)} entregado";
    public string SecondTenderText => $"2do pago: {SecondTenderDescription}";
    public bool CanDelete => IsLocalEntry;
    public bool HasFiscalData => !string.IsNullOrWhiteSpace(Clave50);
    public bool HasDiscount => DiscountColones > 0;
    public bool HasExoneration => ExonerationColones > 0;
    public bool HasChange => ChangeColones > 0;
    public bool HasSecondTender => SecondTenderAmountColones > 0 && !string.IsNullOrWhiteSpace(SecondTenderDescription);

    public void NormalizeForDisplay()
        => SyncLinePresentation();

    private void SyncLinePresentation()
    {
        foreach (var line in _lines)
            line.IsCreditNote = IsCreditNote;
    }

    private string FormatSignedCurrency(decimal amount)
    {
        var displayAmount = IsCreditNote ? -Math.Abs(amount) : amount;
        return $"{UiConfig.CurrencySymbol}{displayAmount:N2}";
    }
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
    public bool IsCreditNote { get; set; }

    public bool HasTax => TaxPercentage > 0;
    public string QuantityText => Quantity.ToString("0.###");
    public string TaxPercentageText => $"{TaxPercentage:0.##} %";
    public string UnitPriceText => $"{UiConfig.CurrencySymbol}{DisplayAmount(UnitPriceColones):N2}";
    public string LineTotalText => $"{UiConfig.CurrencySymbol}{DisplayAmount(LineTotalColones):N2}";
    public string DiscountText => $"Desc. {DiscountPercent:0.##}%";
    public string ExonerationText => $"Exoneracion {ExonerationPercent:0.##}%";

    private decimal DisplayAmount(decimal amount)
        => IsCreditNote ? -Math.Abs(amount) : amount;
}
