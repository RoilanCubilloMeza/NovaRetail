using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.Linq;
using Newtonsoft.Json;

namespace NovaAPI.Models
{
    public class NovaRetailCreateSaleRequest : IValidatableObject
    {
        [Range(1, int.MaxValue)]
        public int StoreID { get; set; }

        [Range(1, int.MaxValue)]
        public int RegisterID { get; set; }

        [Range(1, int.MaxValue)]
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

        [Required]
        public List<NovaRetailSaleItemDto> Items { get; set; } = new List<NovaRetailSaleItemDto>();

        [Required]
        public List<NovaRetailSaleTenderDto> Tenders { get; set; } = new List<NovaRetailSaleTenderDto>();

        public IEnumerable<ValidationResult> Validate(ValidationContext validationContext)
        {
            if (Items == null || Items.Count == 0)
                yield return new ValidationResult("La venta no contiene ítems.", new[] { nameof(Items) });

            if (Tenders == null || Tenders.Count == 0)
                yield return new ValidationResult("La venta no contiene formas de pago.", new[] { nameof(Tenders) });

            if (Items != null && Items.GroupBy(x => x.RowNo).Any(g => g.Count() > 1))
                yield return new ValidationResult("Items contiene RowNo duplicados.", new[] { nameof(Items) });

            if (Tenders != null && Tenders.GroupBy(x => x.RowNo).Any(g => g.Count() > 1))
                yield return new ValidationResult("Tenders contiene RowNo duplicados.", new[] { nameof(Tenders) });
        }
    }

    public class NovaRetailSaleItemDto : IValidatableObject
    {
        [Range(1, int.MaxValue)]
        public int RowNo { get; set; }

        [Range(1, int.MaxValue)]
        public int ItemID { get; set; }

        [Range(typeof(decimal), "0.0001", "999999999999999.9999")]
        public decimal Quantity { get; set; }

        [Range(typeof(decimal), "0", "999999999999999.9999")]
        public decimal UnitPrice { get; set; }

        public decimal? FullPrice { get; set; }
        public decimal Cost { get; set; }
        public decimal Commission { get; set; }
        public int PriceSource { get; set; } = 1;
        public int SalesRepID { get; set; }
        public bool Taxable { get; set; } = true;
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

        public IEnumerable<ValidationResult> Validate(ValidationContext validationContext)
        {
            if (Taxable && !TaxID.HasValue)
                yield return new ValidationResult("TaxID es requerido cuando Taxable es true.", new[] { nameof(TaxID) });

            if (Cost < 0)
                yield return new ValidationResult("Cost no puede ser negativo.", new[] { nameof(Cost) });

            if (Commission < 0)
                yield return new ValidationResult("Commission no puede ser negativo.", new[] { nameof(Commission) });

            if (SalesTax < 0)
                yield return new ValidationResult("SalesTax no puede ser negativo.", new[] { nameof(SalesTax) });

            if (LineDiscountAmount < 0)
                yield return new ValidationResult("LineDiscountAmount no puede ser negativo.", new[] { nameof(LineDiscountAmount) });

            if (LineDiscountPercent < 0 || LineDiscountPercent > 100)
                yield return new ValidationResult("LineDiscountPercent debe estar entre 0 y 100.", new[] { nameof(LineDiscountPercent) });
        }
    }

    public class NovaRetailSaleTenderDto : IValidatableObject
    {
        [Range(1, int.MaxValue)]
        public int RowNo { get; set; }

        [Range(1, int.MaxValue)]
        public int TenderID { get; set; }

        public int PaymentID { get; set; }
        public string Description { get; set; } = string.Empty;

        [Range(typeof(decimal), "0.0001", "999999999999999.9999")]
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

        public IEnumerable<ValidationResult> Validate(ValidationContext validationContext)
        {
            if (string.IsNullOrWhiteSpace(Description))
                yield return new ValidationResult("Description es requerido.", new[] { nameof(Description) });

            if (AmountForeign.HasValue && AmountForeign.Value < 0)
                yield return new ValidationResult("AmountForeign no puede ser negativo.", new[] { nameof(AmountForeign) });

            if (RoundingError < 0)
                yield return new ValidationResult("RoundingError no puede ser negativo.", new[] { nameof(RoundingError) });

            if (DebitSurcharge < 0)
                yield return new ValidationResult("DebitSurcharge no puede ser negativo.", new[] { nameof(DebitSurcharge) });

            if (CashBackSurcharge < 0)
                yield return new ValidationResult("CashBackSurcharge no puede ser negativo.", new[] { nameof(CashBackSurcharge) });
        }
    }

    public class NovaRetailCreateSaleResponse
    {
        [JsonProperty("ok")]
        public bool Ok { get; set; }

        [JsonProperty("message")]
        public string Message { get; set; } = string.Empty;

        [JsonProperty("transactionNumber")]
        public int TransactionNumber { get; set; }

        [JsonProperty("batchNumber")]
        public int? BatchNumber { get; set; }

        [JsonProperty("subTotal")]
        public decimal? SubTotal { get; set; }

        [JsonProperty("discounts")]
        public decimal? Discounts { get; set; }

        [JsonProperty("salesTax")]
        public decimal? SalesTax { get; set; }

        [JsonProperty("total")]
        public decimal? Total { get; set; }

        [JsonProperty("tenderTotal")]
        public decimal? TenderTotal { get; set; }

        [JsonProperty("errorNumber")]
        public int? ErrorNumber { get; set; }

        [JsonProperty("errorProcedure")]
        public string ErrorProcedure { get; set; } = string.Empty;

        [JsonProperty("errorLine")]
        public int? ErrorLine { get; set; }

        [JsonProperty("taxEntriesInserted")]
        public int TaxEntriesInserted { get; set; }

        [JsonProperty("tiqueteEsperaOk")]
        public bool TiqueteEsperaOk { get; set; }

        [JsonProperty("warnings")]
        public List<string> Warnings { get; set; } = new List<string>();
    }
}
