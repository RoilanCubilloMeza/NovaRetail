namespace NovaRetail.Models;

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
}
