using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using Newtonsoft.Json;

namespace NovaAPI.Models
{
    public class NovaRetailCreateQuoteRequest : IValidatableObject
    {
        public int OrderID { get; set; }

        [Range(1, int.MaxValue)]
        public int StoreID { get; set; }

        /// <summary>Order type: 2 = Factura en Espera, 3 = Cotización (default).</summary>
        public int Type { get; set; } = 3;

        public int CustomerID { get; set; }
        public int ShipToID { get; set; }
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

        [Required]
        public List<NovaRetailQuoteItemDto> Items { get; set; } = new List<NovaRetailQuoteItemDto>();

        public IEnumerable<ValidationResult> Validate(ValidationContext validationContext)
        {
            if (Items == null || Items.Count == 0)
                yield return new ValidationResult("La orden no contiene ítems.", new[] { nameof(Items) });

            if (Type != 2 && Type != 3)
                yield return new ValidationResult("Type debe ser 2 (Espera) o 3 (Cotización).", new[] { nameof(Type) });
        }
    }

    public class NovaRetailQuoteItemDto
    {
        [Range(1, int.MaxValue)]
        public int ItemID { get; set; }

        public decimal Cost { get; set; }
        public decimal FullPrice { get; set; }
        public int PriceSource { get; set; } = 1;
        public decimal Price { get; set; }

        [Range(0.0001, (double)decimal.MaxValue)]
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

    public class NovaRetailCreateQuoteResponse
    {
        [JsonProperty("ok")]
        public bool Ok { get; set; }

        [JsonProperty("message")]
        public string Message { get; set; } = string.Empty;

        [JsonProperty("orderID")]
        public int OrderID { get; set; }

        [JsonProperty("tax")]
        public decimal Tax { get; set; }

        [JsonProperty("total")]
        public decimal Total { get; set; }

        [JsonProperty("warnings")]
        public List<string> Warnings { get; set; } = new List<string>();
    }

    public class NovaRetailOrderSummaryDto
    {
        [JsonProperty("orderID")]
        public int OrderID { get; set; }

        [JsonProperty("type")]
        public int Type { get; set; }

        [JsonProperty("comment")]
        public string Comment { get; set; } = string.Empty;

        [JsonProperty("total")]
        public decimal Total { get; set; }

        [JsonProperty("tax")]
        public decimal Tax { get; set; }

        [JsonProperty("time")]
        public DateTime Time { get; set; }

        [JsonProperty("expirationOrDueDate")]
        public DateTime? ExpirationOrDueDate { get; set; }

        [JsonProperty("customerID")]
        public int CustomerID { get; set; }

        [JsonProperty("itemCount")]
        public int ItemCount { get; set; }

        [JsonProperty("referenceNumber")]
        public string ReferenceNumber { get; set; } = string.Empty;

        [JsonProperty("cashierName")]
        public string CashierName { get; set; } = string.Empty;
    }

    public class NovaRetailOrderDetailDto
    {
        [JsonProperty("orderID")]
        public int OrderID { get; set; }

        [JsonProperty("type")]
        public int Type { get; set; }

        [JsonProperty("comment")]
        public string Comment { get; set; } = string.Empty;

        [JsonProperty("total")]
        public decimal Total { get; set; }

        [JsonProperty("tax")]
        public decimal Tax { get; set; }

        [JsonProperty("time")]
        public DateTime Time { get; set; }

        [JsonProperty("entries")]
        public List<NovaRetailOrderEntryDto> Entries { get; set; } = new List<NovaRetailOrderEntryDto>();
    }

    public class NovaRetailOrderEntryDto
    {
        [JsonProperty("entryID")]
        public int EntryID { get; set; }

        [JsonProperty("itemID")]
        public int ItemID { get; set; }

        [JsonProperty("description")]
        public string Description { get; set; } = string.Empty;

        [JsonProperty("price")]
        public decimal Price { get; set; }

        [JsonProperty("fullPrice")]
        public decimal FullPrice { get; set; }

        [JsonProperty("cost")]
        public decimal Cost { get; set; }

        [JsonProperty("quantityOnOrder")]
        public decimal QuantityOnOrder { get; set; }

        [JsonProperty("salesRepID")]
        public int SalesRepID { get; set; }

        [JsonProperty("taxable")]
        public bool Taxable { get; set; }

        [JsonProperty("taxID")]
        public int TaxID { get; set; }

        [JsonProperty("itemType")]
        public int ItemType { get; set; }
    }

    public class NovaRetailListOrdersResponse
    {
        [JsonProperty("ok")]
        public bool Ok { get; set; }

        [JsonProperty("message")]
        public string Message { get; set; } = string.Empty;

        [JsonProperty("orders")]
        public List<NovaRetailOrderSummaryDto> Orders { get; set; } = new List<NovaRetailOrderSummaryDto>();
    }

    public class NovaRetailOrderDetailResponse
    {
        [JsonProperty("ok")]
        public bool Ok { get; set; }

        [JsonProperty("message")]
        public string Message { get; set; } = string.Empty;

        [JsonProperty("order")]
        public NovaRetailOrderDetailDto Order { get; set; }
    }

    public class NovaRetailUpdateOrderRequest
    {
        [Range(1, int.MaxValue)]
        public int OrderID { get; set; }

        public decimal Tax { get; set; }
        public decimal Total { get; set; }
        public string Comment { get; set; } = string.Empty;

        [Required]
        public List<NovaRetailQuoteItemDto> Items { get; set; } = new List<NovaRetailQuoteItemDto>();
    }

    public class NovaRetailUpdateOrderResponse
    {
        [JsonProperty("ok")]
        public bool Ok { get; set; }

        [JsonProperty("message")]
        public string Message { get; set; } = string.Empty;

        [JsonProperty("orderID")]
        public int OrderID { get; set; }
    }
}
