namespace NovaRetail.Models;

/// <summary>
/// Request para crear una venta en el backend (<c>api/NovaRetailSales</c>).
/// Incluye datos de tienda, cajero, cliente, líneas de artículo, formas de pago
/// y campos de facturación electrónica costarricense (clave 50, consecutivo, tipo de comprobante).
/// </summary>
public sealed class NovaRetailCreateSaleRequest
{
    public int StoreID { get; set; }
    public int RegisterID { get; set; }
    public int CashierID { get; set; }
    public int CustomerID { get; set; }
    public int ShipToID { get; set; }
    public string Comment { get; set; } = string.Empty;
    public string ReferenceNumber { get; set; } = string.Empty;
    public int Status { get; set; }
    public int ExchangeID { get; set; }
    public int ChannelType { get; set; }
    public int RecallID { get; set; }
    public int RecallType { get; set; }
    public DateTime? TransactionTime { get; set; }
    public decimal TotalChange { get; set; }
    public bool AllowNegativeInventory { get; set; }
    public string CurrencyCode { get; set; } = "CRC";
    public string TipoCambio { get; set; } = "1";
    public string CondicionVenta { get; set; } = "01";
    public string CodCliente { get; set; } = string.Empty;
    public string NombreCliente { get; set; } = string.Empty;
    public string CedulaTributaria { get; set; } = string.Empty;
    public short Exonera { get; set; }
    public bool InsertarTiqueteEspera { get; set; }
    public string CLAVE50 { get; set; } = string.Empty;
    public string CLAVE20 { get; set; } = string.Empty;
    public string COD_SUCURSAL { get; set; } = string.Empty;
    public string TERMINAL_POS { get; set; } = string.Empty;
    public string COMPROBANTE_INTERNO { get; set; } = string.Empty;
    public string COMPROBANTE_SITUACION { get; set; } = string.Empty;
    public string COMPROBANTE_TIPO { get; set; } = string.Empty;
    public string NC_TIPO_DOC { get; set; } = string.Empty;
    public string NC_REFERENCIA { get; set; } = string.Empty;
    public DateTime? NC_REFERENCIA_FECHA { get; set; }
    public string NC_CODIGO { get; set; } = string.Empty;
    public string NC_RAZON { get; set; } = string.Empty;
    public string TR_REP { get; set; } = string.Empty;
    public List<NovaRetailSaleItemRequest> Items { get; set; } = new();
    public List<NovaRetailSaleTenderRequest> Tenders { get; set; } = new();
}

public sealed class NovaRetailSaleItemRequest
{
    public int RowNo { get; set; }
    public int ItemID { get; set; }
    public decimal Quantity { get; set; }
    public decimal UnitPrice { get; set; }
    public decimal? FullPrice { get; set; }
    public decimal Cost { get; set; }
    public decimal Commission { get; set; }
    public int PriceSource { get; set; } = 1;
    public int SalesRepID { get; set; }
    public bool Taxable { get; set; }
    public int? TaxID { get; set; }
    public decimal SalesTax { get; set; }
    public string LineComment { get; set; } = string.Empty;
    public int DiscountReasonCodeID { get; set; }
    public int ReturnReasonCodeID { get; set; }
    public int TaxChangeReasonCodeID { get; set; }
    public int QuantityDiscountID { get; set; }
    public int ItemType { get; set; }
    public decimal ComputedQuantity { get; set; }
    public bool IsAddMoney { get; set; }
    public int VoucherID { get; set; }
    public string ExtendedDescription { get; set; } = string.Empty;
    public int? PromotionID { get; set; }
    public string PromotionName { get; set; } = string.Empty;
    public decimal LineDiscountAmount { get; set; }
    public decimal LineDiscountPercent { get; set; }
    public string ExTipoDoc { get; set; } = string.Empty;
    public string ExNumeroDoc { get; set; } = string.Empty;
    public string ExInstitucion { get; set; } = string.Empty;
    public DateTime? ExFecha { get; set; }
    public decimal ExPorcentaje { get; set; }
    public decimal ExMonto { get; set; }
}

public sealed class NovaRetailSaleTenderRequest
{
    public int RowNo { get; set; }
    public int TenderID { get; set; }
    public int PaymentID { get; set; }
    public string Description { get; set; } = string.Empty;
    public decimal Amount { get; set; }
    public decimal? AmountForeign { get; set; }
    public decimal RoundingError { get; set; }
    public string CreditCardExpiration { get; set; } = string.Empty;
    public string CreditCardNumber { get; set; } = string.Empty;
    public string CreditCardApprovalCode { get; set; } = string.Empty;
    public string AccountHolder { get; set; } = string.Empty;
    public string BankNumber { get; set; } = string.Empty;
    public string SerialNumber { get; set; } = string.Empty;
    public string State { get; set; } = string.Empty;
    public string License { get; set; } = string.Empty;
    public DateTime? BirthDate { get; set; }
    public string TransitNumber { get; set; } = string.Empty;
    public int VisaNetAuthorizationID { get; set; }
    public decimal DebitSurcharge { get; set; }
    public decimal CashBackSurcharge { get; set; }
    public bool IsCreateNew { get; set; }
    public string MedioPagoCodigo { get; set; } = string.Empty;
}

public sealed class NovaRetailCreateSaleResponse
{
    public bool Ok { get; set; }
    public string Message { get; set; } = string.Empty;
    public int TransactionNumber { get; set; }
    public int? BatchNumber { get; set; }
    public decimal? SubTotal { get; set; }
    public decimal? Discounts { get; set; }
    public decimal? SalesTax { get; set; }
    public decimal? Total { get; set; }
    public decimal? TenderTotal { get; set; }
    public int? ErrorNumber { get; set; }
    public string ErrorProcedure { get; set; } = string.Empty;
    public int? ErrorLine { get; set; }
    public int TaxEntriesInserted { get; set; }
    public bool TiqueteEsperaOk { get; set; }
    public string Clave50 { get; set; } = string.Empty;
    public string Clave20 { get; set; } = string.Empty;
    public List<string> Warnings { get; set; } = new();
}

public sealed class NovaRetailInvoiceHistorySearchResponse
{
    public bool Ok { get; set; }
    public string Message { get; set; } = string.Empty;
    public List<NovaRetailInvoiceHistoryEntryDto> Entries { get; set; } = new();
}

public sealed class NovaRetailInvoiceHistoryDetailResponse
{
    public bool Ok { get; set; }
    public string Message { get; set; } = string.Empty;
    public NovaRetailInvoiceHistoryEntryDto? Entry { get; set; }
}

public sealed class NovaRetailInvoiceHistoryEntryDto
{
    public int TransactionNumber { get; set; }
    public DateTime Date { get; set; }
    public string ComprobanteTipo { get; set; } = string.Empty;
    public string Clave50 { get; set; } = string.Empty;
    public string Consecutivo { get; set; } = string.Empty;
    public string ClientId { get; set; } = string.Empty;
    public string ClientName { get; set; } = string.Empty;
    public string CashierName { get; set; } = string.Empty;
    public int RegisterNumber { get; set; }
    public string StoreName { get; set; } = string.Empty;
    public decimal SubtotalColones { get; set; }
    public decimal DiscountColones { get; set; }
    public decimal ExonerationColones { get; set; }
    public decimal TaxColones { get; set; }
    public decimal TotalColones { get; set; }
    public decimal ChangeColones { get; set; }
    public string TenderDescription { get; set; } = string.Empty;
    public decimal TenderTotalColones { get; set; }
    public string SecondTenderDescription { get; set; } = string.Empty;
    public decimal SecondTenderAmountColones { get; set; }
    public List<NovaRetailInvoiceHistoryLineDto> Lines { get; set; } = new();
}

public sealed class NovaRetailInvoiceHistoryLineDto
{
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
}
