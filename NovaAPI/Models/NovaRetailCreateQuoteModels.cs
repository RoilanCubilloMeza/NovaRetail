using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using Newtonsoft.Json;

namespace NovaAPI.Models
{
    /// <summary>
    /// Request para crear o actualizar una cotización/factura en espera.
    /// Se usa desde el POS cuando el carrito no se convierte todavía en venta final.
    /// </summary>
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

    /// <summary>
    /// Línea de una cotización o hold.
    /// Contiene el artículo, precio, cantidad, impuestos y reason codes necesarios
    /// para persistir la orden temporal en backend.
    /// </summary>
    public class NovaRetailQuoteItemDto
    {
        [Range(1, int.MaxValue)]
        public int ItemID { get; set; }

        public decimal Cost { get; set; }
        public decimal FullPrice { get; set; }
        public int PriceSource { get; set; } = 1;
        public decimal Price { get; set; }

        [Range(typeof(decimal), "0.0001", "999999999999999.9999")]
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

    /// <summary>
    /// Respuesta al guardar una cotización o factura en espera.
    /// Devuelve el identificador de la orden y posibles advertencias funcionales.
    /// </summary>
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

    /// <summary>
    /// Resumen de una orden para listados de búsqueda.
    /// Se usa en el popup de recuperación para no cargar el detalle completo hasta que sea necesario.
    /// </summary>
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

    /// <summary>
    /// Encabezado detallado de una cotización o hold recuperado desde backend.
    /// Incluye sus líneas completas para volver a cargar el carrito en la app.
    /// </summary>
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

    /// <summary>
    /// Línea individual de una orden recuperada.
    /// Transporta precios, cantidad e impuesto al frontend para reconstruir el carrito.
    /// </summary>
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

        [JsonProperty("taxable")]
        public bool Taxable { get; set; }

        [JsonProperty("taxID")]
        public int TaxID { get; set; }
    }

    /// <summary>
    /// Respuesta de listado de órdenes.
    /// Entrega una colección resumida de cotizaciones o holds filtrados por tienda y texto de búsqueda.
    /// </summary>
    public class NovaRetailListOrdersResponse
    {
        [JsonProperty("ok")]
        public bool Ok { get; set; }

        [JsonProperty("message")]
        public string Message { get; set; } = string.Empty;

        [JsonProperty("orders")]
        public List<NovaRetailOrderSummaryDto> Orders { get; set; } = new List<NovaRetailOrderSummaryDto>();
    }

    /// <summary>
    /// Respuesta del detalle completo de una orden.
    /// Se usa cuando el usuario selecciona una cotización o hold para recuperarlo en el carrito.
    /// </summary>
    public class NovaRetailOrderDetailResponse
    {
        [JsonProperty("ok")]
        public bool Ok { get; set; }

        [JsonProperty("message")]
        public string Message { get; set; } = string.Empty;

        [JsonProperty("order")]
        public NovaRetailOrderDetailDto Order { get; set; }
    }

    /// <summary>
    /// Request simplificado para actualizar una orden existente.
    /// Se usa cuando una cotización o hold ya guardado debe reemplazar sus líneas y totales.
    /// </summary>
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

    /// <summary>
    /// Respuesta al actualizar una orden existente.
    /// Mantiene un contrato ligero para confirmar éxito y mensajes de validación.
    /// </summary>
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
