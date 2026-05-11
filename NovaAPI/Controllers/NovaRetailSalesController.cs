using System;
using System.Collections.Generic;
using System.Configuration;
using System.Data;
using System.Data.SqlClient;
using System.Diagnostics;
using System.Globalization;
using System.Linq;
using System.Net;
using System.Net.Http;
using System.IO;
using System.Web.Http;
using System.Xml.Linq;
using NovaAPI.Models;
using NovaAPI.Services;

namespace NovaAPI.Controllers
{
    [RoutePrefix("api/NovaRetailSales")]
    public partial class NovaRetailSalesController : ApiController
    {
        private const int ReferenceNumberMaxLength = 50;
        private const int HoldRecallType = 1;
        private const int QuoteRecallType = 3;
        private const int WorkOrderType = 2;
        private const int QuoteOrderType = 3;
        private const decimal LedgerClosingTolerance = 0.01m;
        private static readonly object SalePerformanceIndexesLock = new object();
        private static bool _salePerformanceIndexesChecked;

        private static readonly string ErrorLogPath =
            System.IO.Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "nova_error.log");

        private static readonly string PerformanceLogPath =
            System.IO.Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "nova_perf.log");

        private static void LogError(Exception ex)
        {
            try
            {
                NovaFileLogger.AppendLine(ErrorLogPath, $"[{DateTime.Now:yyyy-MM-dd HH:mm:ss}] {ex}\r\n");
            }
            catch { }
        }

        private static void LogPerformance(string message)
        {
            try
            {
                NovaFileLogger.AppendLine(PerformanceLogPath, $"[{DateTime.Now:yyyy-MM-dd HH:mm:ss.fff}] {message}");
            }
            catch { }
        }

        private static void LogOrderSavedAudit(SqlConnection cn, NovaRetailCreateQuoteRequest request, int orderId, string actionType, string entityType)
        {
            NovaRetailAuditLogger.Log(
                cn,
                actionType,
                entityType,
                orderId,
                request.CashierID > 0 ? request.CashierID : request.SalesRepID,
                request.StoreID,
                request.RegisterID,
                request.Total,
                $"{entityType} #{orderId}");

            LogQuoteItemAudit(cn, request, orderId, entityType);
        }

        private static void LogQuoteItemAudit(SqlConnection cn, NovaRetailCreateQuoteRequest request, int orderId, string entityType)
        {
            if (request == null || request.Items == null)
                return;

            var cashierID = request.CashierID > 0 ? request.CashierID : request.SalesRepID;
            foreach (var item in request.Items)
            {
                if (item == null)
                    continue;

                if (item.FullPrice > item.Price)
                {
                    NovaRetailAuditLogger.Log(
                        cn,
                        "DiscountApplied",
                        entityType,
                        orderId,
                        cashierID,
                        request.StoreID,
                        request.RegisterID,
                        item.FullPrice - item.Price,
                        $"Item {item.ItemID}: descuento/precio menor de {item.FullPrice:N2} a {item.Price:N2}. Motivo {item.DiscountReasonCodeID}");
                }
                else if (item.PriceSource != 1 || item.Price > item.FullPrice)
                {
                    NovaRetailAuditLogger.Log(
                        cn,
                        "PriceChanged",
                        entityType,
                        orderId,
                        cashierID,
                        request.StoreID,
                        request.RegisterID,
                        item.Price,
                        $"Item {item.ItemID}: precio cambiado de {item.FullPrice:N2} a {item.Price:N2}. PriceSource {item.PriceSource}");
                }
            }
        }

        private static void LogSaleItemAudit(SqlConnection cn, NovaRetailCreateSaleRequest request, int transactionNumber)
        {
            if (request == null || request.Items == null)
                return;

            var entries = new List<NovaRetailAuditLogger.AuditEntry>();
            foreach (var item in request.Items)
            {
                if (item == null)
                    continue;

                var fullPrice = item.DisplayFullPrice ?? item.FullPrice ?? item.UnitPrice;
                var price = item.DisplayPrice ?? item.UnitPrice;
                if (fullPrice > price || item.DiscountReasonCodeID > 0)
                {
                    entries.Add(new NovaRetailAuditLogger.AuditEntry
                    {
                        ActionType = "DiscountApplied",
                        EntityType = "Sale",
                        EntityID = transactionNumber,
                        CashierID = request.CashierID,
                        StoreID = request.StoreID,
                        RegisterID = request.RegisterID,
                        Amount = Math.Max(0m, fullPrice - price),
                        Detail = $"Item {item.ItemID}: descuento/precio menor de {fullPrice:N2} a {price:N2}. Motivo {item.DiscountReasonCodeID}"
                    });
                }
                else if (item.PriceSource != 1 || price > fullPrice)
                {
                    entries.Add(new NovaRetailAuditLogger.AuditEntry
                    {
                        ActionType = "PriceChanged",
                        EntityType = "Sale",
                        EntityID = transactionNumber,
                        CashierID = request.CashierID,
                        StoreID = request.StoreID,
                        RegisterID = request.RegisterID,
                        Amount = price,
                        Detail = $"Item {item.ItemID}: precio cambiado de {fullPrice:N2} a {price:N2}. PriceSource {item.PriceSource}"
                    });
                }
            }

            NovaRetailAuditLogger.LogMany(cn, entries);
        }

        private static string GetConnectionString()
        {
            var connectionString = AppConfig.ConnectionString("RMHPOS");
            if (string.IsNullOrWhiteSpace(connectionString))
                throw new ConfigurationErrorsException("No se encontrÃƒÆ’Ã‚Â³ la cadena de conexiÃƒÆ’Ã‚Â³n RMHPOS para registrar ventas.");

            return connectionString;
        }

        private static string GetConnectionTarget(string connectionString)
        {
            try
            {
                var builder = new SqlConnectionStringBuilder(connectionString);
                return $"{builder.DataSource} / {builder.InitialCatalog}";
            }
            catch
            {
                return "RMHPOS";
            }
        }

        private static void EnsureSalePerformanceIndexes(SqlConnection cn)
        {
            if (_salePerformanceIndexesChecked)
                return;

            lock (SalePerformanceIndexesLock)
            {
                if (_salePerformanceIndexesChecked)
                    return;

                try
                {
                    using (var cmd = new SqlCommand(@"
IF OBJECT_ID(N'dbo.TaxEntry', N'U') IS NOT NULL
   AND NOT EXISTS (SELECT 1 FROM sys.indexes WHERE name = N'IX_TaxEntry_TransactionEntryID' AND object_id = OBJECT_ID(N'dbo.TaxEntry', N'U'))
    CREATE NONCLUSTERED INDEX IX_TaxEntry_TransactionEntryID ON dbo.TaxEntry (TransactionEntryID);

IF OBJECT_ID(N'dbo.TaxEntry', N'U') IS NOT NULL
   AND NOT EXISTS (SELECT 1 FROM sys.indexes WHERE name = N'IX_TaxEntry_TransactionNumber' AND object_id = OBJECT_ID(N'dbo.TaxEntry', N'U'))
    CREATE NONCLUSTERED INDEX IX_TaxEntry_TransactionNumber ON dbo.TaxEntry (TransactionNumber);

IF OBJECT_ID(N'dbo.TransactionEntry', N'U') IS NOT NULL
   AND NOT EXISTS (SELECT 1 FROM sys.indexes WHERE name = N'IX_TransactionEntry_TransactionNumber_DetailID' AND object_id = OBJECT_ID(N'dbo.TransactionEntry', N'U'))
    CREATE NONCLUSTERED INDEX IX_TransactionEntry_TransactionNumber_DetailID ON dbo.TransactionEntry (TransactionNumber, DetailID) INCLUDE (ID, ItemID);

IF OBJECT_ID(N'dbo.AVS_INTEGRAFAST_01', N'U') IS NOT NULL
   AND NOT EXISTS (SELECT 1 FROM sys.indexes WHERE name = N'IX_AVS_INTEGRAFAST_01_TRANSACTIONNUMBER' AND object_id = OBJECT_ID(N'dbo.AVS_INTEGRAFAST_01', N'U'))
    CREATE NONCLUSTERED INDEX IX_AVS_INTEGRAFAST_01_TRANSACTIONNUMBER ON dbo.AVS_INTEGRAFAST_01 (TRANSACTIONNUMBER);

IF OBJECT_ID(N'dbo.AVS_INTEGRAFAST_05', N'U') IS NOT NULL
   AND NOT EXISTS (SELECT 1 FROM sys.indexes WHERE name = N'IX_AVS_INTEGRAFAST_05_CLAVE50' AND object_id = OBJECT_ID(N'dbo.AVS_INTEGRAFAST_05', N'U'))
    CREATE NONCLUSTERED INDEX IX_AVS_INTEGRAFAST_05_CLAVE50 ON dbo.AVS_INTEGRAFAST_05 (CLAVE50);

IF OBJECT_ID(N'dbo.[Transaction]', N'U') IS NOT NULL
   AND NOT EXISTS (SELECT 1 FROM sys.indexes WHERE name = N'IX_Transaction_Time_History' AND object_id = OBJECT_ID(N'dbo.[Transaction]', N'U'))
    CREATE NONCLUSTERED INDEX IX_Transaction_Time_History ON dbo.[Transaction] ([Time] DESC) INCLUDE (TransactionNumber, Total, SalesTax, CustomerID);

IF OBJECT_ID(N'dbo.[Transaction]', N'U') IS NOT NULL
   AND NOT EXISTS (SELECT 1 FROM sys.indexes WHERE name = N'IX_Transaction_CustomerID_History' AND object_id = OBJECT_ID(N'dbo.[Transaction]', N'U'))
    CREATE NONCLUSTERED INDEX IX_Transaction_CustomerID_History ON dbo.[Transaction] (CustomerID) INCLUDE (TransactionNumber, [Time], Total, SalesTax);

IF OBJECT_ID(N'dbo.Customer', N'U') IS NOT NULL
   AND NOT EXISTS (SELECT 1 FROM sys.indexes WHERE name = N'IX_Customer_AccountNumber_History' AND object_id = OBJECT_ID(N'dbo.Customer', N'U'))
    CREATE NONCLUSTERED INDEX IX_Customer_AccountNumber_History ON dbo.Customer (AccountNumber) INCLUDE (ID, FirstName, LastName);

IF OBJECT_ID(N'dbo.AVS_INTEGRAFAST_01', N'U') IS NOT NULL
   AND NOT EXISTS (SELECT 1 FROM sys.indexes WHERE name = N'IX_AVS_INTEGRAFAST_01_Search_History' AND object_id = OBJECT_ID(N'dbo.AVS_INTEGRAFAST_01', N'U'))
    CREATE NONCLUSTERED INDEX IX_AVS_INTEGRAFAST_01_Search_History ON dbo.AVS_INTEGRAFAST_01 (TRANSACTIONNUMBER) INCLUDE (CEDULA_TRIBUTARIA, NOMBRE_CLIENTE, CLAVE20, COMPROBANTE_INTERNO, CLAVE50, COMPROBANTE_TIPO);

IF OBJECT_ID(N'dbo.AVS_INTEGRAFAST_01', N'U') IS NOT NULL
   AND NOT EXISTS (SELECT 1 FROM sys.indexes WHERE name = N'IX_AVS_INTEGRAFAST_01_CLAVE50_LedgerLookup' AND object_id = OBJECT_ID(N'dbo.AVS_INTEGRAFAST_01', N'U'))
    CREATE NONCLUSTERED INDEX IX_AVS_INTEGRAFAST_01_CLAVE50_LedgerLookup ON dbo.AVS_INTEGRAFAST_01 (CLAVE50) INCLUDE (TRANSACTIONNUMBER, CLAVE20, COMPROBANTE_INTERNO);

IF OBJECT_ID(N'dbo.AVS_INTEGRAFAST_01', N'U') IS NOT NULL
   AND NOT EXISTS (SELECT 1 FROM sys.indexes WHERE name = N'IX_AVS_INTEGRAFAST_01_CLAVE20_LedgerLookup' AND object_id = OBJECT_ID(N'dbo.AVS_INTEGRAFAST_01', N'U'))
    CREATE NONCLUSTERED INDEX IX_AVS_INTEGRAFAST_01_CLAVE20_LedgerLookup ON dbo.AVS_INTEGRAFAST_01 (CLAVE20) INCLUDE (TRANSACTIONNUMBER, CLAVE50, COMPROBANTE_INTERNO);

IF OBJECT_ID(N'dbo.AVS_INTEGRAFAST_01', N'U') IS NOT NULL
   AND NOT EXISTS (SELECT 1 FROM sys.indexes WHERE name = N'IX_AVS_INTEGRAFAST_01_INTERNO_LedgerLookup' AND object_id = OBJECT_ID(N'dbo.AVS_INTEGRAFAST_01', N'U'))
    CREATE NONCLUSTERED INDEX IX_AVS_INTEGRAFAST_01_INTERNO_LedgerLookup ON dbo.AVS_INTEGRAFAST_01 (COMPROBANTE_INTERNO) INCLUDE (TRANSACTIONNUMBER, CLAVE50, CLAVE20);

IF OBJECT_ID(N'dbo.AR_LedgerEntry', N'U') IS NOT NULL
   AND NOT EXISTS (SELECT 1 FROM sys.indexes WHERE name = N'IX_AR_LedgerEntry_Account_DocumentID_Lookup' AND object_id = OBJECT_ID(N'dbo.AR_LedgerEntry', N'U'))
    CREATE NONCLUSTERED INDEX IX_AR_LedgerEntry_Account_DocumentID_Lookup ON dbo.AR_LedgerEntry (AccountID, DocumentID) INCLUDE (ID, DocumentType, [Open], Reference, ExtReference);

IF OBJECT_ID(N'dbo.AR_LedgerEntry', N'U') IS NOT NULL
   AND NOT EXISTS (SELECT 1 FROM sys.indexes WHERE name = N'IX_AR_LedgerEntry_Account_Reference_Lookup' AND object_id = OBJECT_ID(N'dbo.AR_LedgerEntry', N'U'))
    CREATE NONCLUSTERED INDEX IX_AR_LedgerEntry_Account_Reference_Lookup ON dbo.AR_LedgerEntry (AccountID, Reference) INCLUDE (ID, DocumentID, DocumentType, [Open], ExtReference);

IF OBJECT_ID(N'dbo.AR_LedgerEntry', N'U') IS NOT NULL
   AND NOT EXISTS (SELECT 1 FROM sys.indexes WHERE name = N'IX_AR_LedgerEntry_Account_ExtReference_Lookup' AND object_id = OBJECT_ID(N'dbo.AR_LedgerEntry', N'U'))
    CREATE NONCLUSTERED INDEX IX_AR_LedgerEntry_Account_ExtReference_Lookup ON dbo.AR_LedgerEntry (AccountID, ExtReference) INCLUDE (ID, DocumentID, DocumentType, [Open], Reference);

IF OBJECT_ID(N'dbo.AR_LedgerEntry', N'U') IS NOT NULL
   AND NOT EXISTS (SELECT 1 FROM sys.indexes WHERE name = N'IX_AR_LedgerEntry_DocumentID_Account_Lookup' AND object_id = OBJECT_ID(N'dbo.AR_LedgerEntry', N'U'))
    CREATE NONCLUSTERED INDEX IX_AR_LedgerEntry_DocumentID_Account_Lookup ON dbo.AR_LedgerEntry (DocumentID) INCLUDE (ID, AccountID, DocumentType, [Open], Reference, ExtReference);

IF OBJECT_ID(N'dbo.AR_LedgerEntry', N'U') IS NOT NULL
   AND NOT EXISTS (SELECT 1 FROM sys.indexes WHERE name = N'IX_AR_LedgerEntry_Reference_Account_Lookup' AND object_id = OBJECT_ID(N'dbo.AR_LedgerEntry', N'U'))
    CREATE NONCLUSTERED INDEX IX_AR_LedgerEntry_Reference_Account_Lookup ON dbo.AR_LedgerEntry (Reference) INCLUDE (ID, AccountID, DocumentID, DocumentType, [Open], ExtReference);

IF OBJECT_ID(N'dbo.AR_LedgerEntry', N'U') IS NOT NULL
   AND NOT EXISTS (SELECT 1 FROM sys.indexes WHERE name = N'IX_AR_LedgerEntry_ExtReference_Account_Lookup' AND object_id = OBJECT_ID(N'dbo.AR_LedgerEntry', N'U'))
    CREATE NONCLUSTERED INDEX IX_AR_LedgerEntry_ExtReference_Account_Lookup ON dbo.AR_LedgerEntry (ExtReference) INCLUDE (ID, AccountID, DocumentID, DocumentType, [Open], Reference);

IF OBJECT_ID(N'dbo.AR_LedgerEntryDetail', N'U') IS NOT NULL
   AND NOT EXISTS (SELECT 1 FROM sys.indexes WHERE name = N'IX_AR_LedgerEntryDetail_LedgerEntry_Applied_Amount' AND object_id = OBJECT_ID(N'dbo.AR_LedgerEntryDetail', N'U'))
    CREATE NONCLUSTERED INDEX IX_AR_LedgerEntryDetail_LedgerEntry_Applied_Amount ON dbo.AR_LedgerEntryDetail (LedgerEntryID, AppliedEntryID) INCLUDE (Amount, AppliedAmount);

IF OBJECT_ID(N'dbo.AR_LedgerEntryDetail', N'U') IS NOT NULL
   AND NOT EXISTS (SELECT 1 FROM sys.indexes WHERE name = N'IX_AR_LedgerEntryDetail_AppliedEntry_Amount' AND object_id = OBJECT_ID(N'dbo.AR_LedgerEntryDetail', N'U'))
    CREATE NONCLUSTERED INDEX IX_AR_LedgerEntryDetail_AppliedEntry_Amount ON dbo.AR_LedgerEntryDetail (AppliedEntryID) INCLUDE (LedgerEntryID, Amount, AppliedAmount);

IF OBJECT_ID(N'dbo.AR_LedgerEntryDetail', N'U') IS NOT NULL
   AND NOT EXISTS (SELECT 1 FROM sys.indexes WHERE name = N'IX_AR_LedgerEntryDetail_BaseBalance_Filtered' AND object_id = OBJECT_ID(N'dbo.AR_LedgerEntryDetail', N'U'))
    CREATE NONCLUSTERED INDEX IX_AR_LedgerEntryDetail_BaseBalance_Filtered ON dbo.AR_LedgerEntryDetail (LedgerEntryID) INCLUDE (Amount) WHERE AppliedEntryID = 0;

IF OBJECT_ID(N'dbo.AR_LedgerEntryDetail', N'U') IS NOT NULL
   AND NOT EXISTS (SELECT 1 FROM sys.indexes WHERE name = N'IX_AR_LedgerEntryDetail_AppliedBalance_Filtered' AND object_id = OBJECT_ID(N'dbo.AR_LedgerEntryDetail', N'U'))
    CREATE NONCLUSTERED INDEX IX_AR_LedgerEntryDetail_AppliedBalance_Filtered ON dbo.AR_LedgerEntryDetail (AppliedEntryID) INCLUDE (AppliedAmount) WHERE AppliedEntryID > 0;", cn))
                    {
                        cmd.CommandTimeout = 300;
                        cmd.ExecuteNonQuery();
                    }
                }
                catch (Exception ex)
                {
                    LogError(new InvalidOperationException("No se pudieron verificar los indices de rendimiento de ventas.", ex));
                }
                finally
                {
                    _salePerformanceIndexesChecked = true;
                }
            }
        }

        private static string SanitizeReferenceNumber(string referenceNumber)
        {
            if (string.IsNullOrWhiteSpace(referenceNumber))
                return string.Empty;

            var value = referenceNumber.Trim();
            return value.Length <= ReferenceNumberMaxLength
                ? value
                : value.Substring(0, ReferenceNumberMaxLength).TrimEnd();
        }

        private static string NormalizeCreditNoteReasonCode(string code)
        {
            var value = (code ?? string.Empty).Trim();
            if (string.IsNullOrWhiteSpace(value))
                return string.Empty;

            var separatorIndex = value.IndexOf('_');
            if (separatorIndex > 0)
                value = value.Substring(0, separatorIndex);

            return value.Length <= 2 ? value : value.Substring(0, 2);
        }

        private static void NormalizeCreditNoteRequest(NovaRetailCreateSaleRequest request)
        {
            if (request == null)
                return;

            request.ReferenceNumber = SanitizeReferenceNumber(request.ReferenceNumber);
            request.NC_REFERENCIA = SanitizeReferenceNumber(request.NC_REFERENCIA);
            request.NC_CODIGO = NormalizeCreditNoteReasonCode(request.NC_CODIGO);

            if (!string.IsNullOrWhiteSpace(request.COMPROBANTE_TIPO))
                request.InsertarTiqueteEspera = true;

            var isManualCreditNote =
                string.Equals(request.COMPROBANTE_TIPO, "03", StringComparison.OrdinalIgnoreCase) &&
                string.Equals(request.NC_TIPO_DOC, "04", StringComparison.OrdinalIgnoreCase);

            if (isManualCreditNote && string.IsNullOrWhiteSpace(request.NC_REFERENCIA))
                request.NC_REFERENCIA = request.ReferenceNumber;
        }

        private static bool RequiresFiscalDocument(NovaRetailCreateSaleRequest request)
        {
            return request != null &&
                   (!string.IsNullOrWhiteSpace(request.COMPROBANTE_TIPO) ||
                    request.InsertarTiqueteEspera);
        }

        [HttpPost]
        [Route("create-sale")]
        public HttpResponseMessage CreateSale([FromBody] NovaRetailCreateSaleRequest request)
        {
            if (request == null)
            {
                return Request.CreateResponse(HttpStatusCode.BadRequest, new NovaRetailCreateSaleResponse
                {
                    Ok = false,
                    Message = "Solicitud invÃƒÆ’Ã‚Â¡lida."
                });
            }

            NormalizeCreditNoteRequest(request);
            var perfRequestStarted = Stopwatch.StartNew();
            LogPerformance($"CreateSale start COMPROBANTE_TIPO={request.COMPROBANTE_TIPO ?? string.Empty} NC_TIPO_DOC={request.NC_TIPO_DOC ?? string.Empty} CondicionVenta={request.CondicionVenta ?? string.Empty} Items={(request.Items == null ? 0 : request.Items.Count)}");

            if (!ModelState.IsValid)
            {
                var errors = ModelState
                    .Where(x => x.Value != null && x.Value.Errors.Count > 0)
                    .SelectMany(x => x.Value.Errors.Select(e => string.IsNullOrWhiteSpace(x.Key)
                        ? e.ErrorMessage
                        : $"{x.Key}: {e.ErrorMessage}"))
                    .ToList();

                return Request.CreateResponse(HttpStatusCode.BadRequest, new NovaRetailCreateSaleResponse
                {
                    Ok = false,
                    Message = errors.Count == 0
                        ? "Solicitud invÃƒÆ’Ã‚Â¡lida."
                        : string.Join(" | ", errors)
                });
            }

            if (request.Items == null || request.Items.Count == 0)
            {
                return Request.CreateResponse(HttpStatusCode.BadRequest, new NovaRetailCreateSaleResponse
                {
                    Ok = false,
                    Message = "La venta no contiene ÃƒÆ’Ã‚Â­tems."
                });
            }

            if (request.Tenders == null || request.Tenders.Count == 0)
            {
                return Request.CreateResponse(HttpStatusCode.BadRequest, new NovaRetailCreateSaleResponse
                {
                    Ok = false,
                    Message = "La venta no contiene formas de pago."
                });
            }

            var response = new NovaRetailCreateSaleResponse();
            var connectionString = GetConnectionString();
            SqlTransaction saleTransaction = null;

            try
            {
                using (var cn = new SqlConnection(connectionString))
                {
                    var perfConnection = Stopwatch.StartNew();
                    cn.Open();
                    LogPerformance($"CreateSale connection open {perfConnection.ElapsedMilliseconds} ms");

                    perfConnection.Restart();
                    EnsureSalePerformanceIndexes(cn);
                    LogPerformance($"CreateSale sale indexes {perfConnection.ElapsedMilliseconds} ms");

                    perfConnection.Restart();
                    var nonInventoryItemTypes = request.AllowNegativeInventory
                        ? new HashSet<int>()
                        : LoadNonInventoryItemTypes(cn);
                    LogPerformance($"CreateSale load non-inventory types {perfConnection.ElapsedMilliseconds} ms");
                    var requiresNonInventoryBypass = !request.AllowNegativeInventory
                        && nonInventoryItemTypes.Count > 0
                        && RequestContainsNonInventoryItems(request.Items, nonInventoryItemTypes);

                    if (requiresNonInventoryBypass)
                    {
                        perfConnection.Restart();
                        saleTransaction = cn.BeginTransaction(IsolationLevel.Serializable);

                        var stockValidation = ValidateInventoryItems(cn, saleTransaction, request.Items, nonInventoryItemTypes);
                        LogPerformance($"CreateSale inventory validation {perfConnection.ElapsedMilliseconds} ms stockOk={stockValidation.StockOk} nonInventory={stockValidation.HasNonInventoryItems}");
                        if (!stockValidation.StockOk)
                        {
                            SafeRollback(saleTransaction);
                            saleTransaction = null;

                            response.Ok = false;
                            response.Message = "Stock insuficiente para uno o mas articulos.";
                            return Request.CreateResponse(HttpStatusCode.BadRequest, response);
                        }

                        requiresNonInventoryBypass = stockValidation.HasNonInventoryItems;
                    }

                    perfConnection.Restart();
                    var activeBatch = ResolveActiveBatch(cn, request.StoreID, request.RegisterID, saleTransaction);
                    LogPerformance($"CreateSale resolve active batch {perfConnection.ElapsedMilliseconds} ms batch={(activeBatch == null ? 0 : activeBatch.BatchNumber)}");
                    if (activeBatch == null)
                    {
                        SafeRollback(saleTransaction);
                        saleTransaction = null;

                        response.Ok = false;
                        response.Message = "No existe un lote/caja abierto para registrar la venta.";
                        return Request.CreateResponse(HttpStatusCode.BadRequest, response);
                    }

                    request.StoreID = activeBatch.StoreID;
                    request.RegisterID = activeBatch.RegisterID;

                    using (var cmd = new SqlCommand("dbo.spNovaRetail_CreateSale", cn))
                    {
                        if (saleTransaction != null)
                            cmd.Transaction = saleTransaction;

                        cmd.CommandType = CommandType.StoredProcedure;
                        cmd.CommandTimeout = 180;

                        cmd.Parameters.AddWithValue("@StoreID", request.StoreID);
                        cmd.Parameters.AddWithValue("@RegisterID", request.RegisterID);
                        cmd.Parameters.AddWithValue("@CashierID", request.CashierID);
                        cmd.Parameters.AddWithValue("@CustomerID", request.CustomerID);
                        cmd.Parameters.AddWithValue("@ShipToID", request.ShipToID);
                        cmd.Parameters.AddWithValue("@Comment", request.Comment ?? string.Empty);
                        cmd.Parameters.AddWithValue("@ReferenceNumber", SanitizeReferenceNumber(request.ReferenceNumber));
                        cmd.Parameters.AddWithValue("@Status", request.Status);
                        cmd.Parameters.AddWithValue("@ExchangeID", request.ExchangeID);
                        cmd.Parameters.AddWithValue("@ChannelType", request.ChannelType);
                        cmd.Parameters.AddWithValue("@RecallID", request.RecallID);
                        cmd.Parameters.AddWithValue("@RecallType", request.RecallType);
                        cmd.Parameters.AddWithValue("@TransactionTime", (object)request.TransactionTime ?? DBNull.Value);
                        cmd.Parameters.AddWithValue("@TotalChange", request.TotalChange);
                        cmd.Parameters.AddWithValue("@AllowNegativeInventory", request.AllowNegativeInventory || requiresNonInventoryBypass);
                        cmd.Parameters.AddWithValue("@CurrencyCode", request.CurrencyCode ?? "CRC");
                        cmd.Parameters.AddWithValue("@TipoCambio", request.TipoCambio ?? "1");
                        cmd.Parameters.AddWithValue("@CondicionVenta", request.CondicionVenta ?? "01");
                        cmd.Parameters.AddWithValue("@CodCliente", request.CodCliente ?? string.Empty);
                        cmd.Parameters.AddWithValue("@NombreCliente", request.NombreCliente ?? string.Empty);
                        cmd.Parameters.AddWithValue("@CedulaTributaria", request.CedulaTributaria ?? string.Empty);
                        cmd.Parameters.AddWithValue("@Exonera", request.Exonera);
                        cmd.Parameters.AddWithValue("@InsertarTiqueteEspera", request.InsertarTiqueteEspera);
                        cmd.Parameters.AddWithValue("@CLAVE50", request.CLAVE50 ?? string.Empty);
                        cmd.Parameters.AddWithValue("@CLAVE20", request.CLAVE20 ?? string.Empty);
                        cmd.Parameters.AddWithValue("@COD_SUCURSAL", request.COD_SUCURSAL ?? string.Empty);
                        cmd.Parameters.AddWithValue("@TERMINAL_POS", request.TERMINAL_POS ?? string.Empty);
                        cmd.Parameters.AddWithValue("@COMPROBANTE_INTERNO", request.COMPROBANTE_INTERNO ?? string.Empty);
                        cmd.Parameters.AddWithValue("@COMPROBANTE_SITUACION", request.COMPROBANTE_SITUACION ?? string.Empty);
                        cmd.Parameters.AddWithValue("@COMPROBANTE_TIPO", request.COMPROBANTE_TIPO ?? string.Empty);
                        cmd.Parameters.AddWithValue("@NC_TIPO_DOC", request.NC_TIPO_DOC ?? string.Empty);
                        cmd.Parameters.AddWithValue("@NC_REFERENCIA", request.NC_REFERENCIA ?? string.Empty);
                        cmd.Parameters.AddWithValue("@NC_REFERENCIA_FECHA", (object)request.NC_REFERENCIA_FECHA ?? DBNull.Value);
                        cmd.Parameters.AddWithValue("@NC_CODIGO", request.NC_CODIGO ?? string.Empty);
                        cmd.Parameters.AddWithValue("@NC_RAZON", request.NC_RAZON ?? string.Empty);
                        cmd.Parameters.AddWithValue("@TR_REP", request.TR_REP ?? string.Empty);

                        var itemsParameter = cmd.Parameters.AddWithValue("@Items", NovaRetailSalesSqlMapper.ToItemsTable(request.Items));
                        itemsParameter.SqlDbType = SqlDbType.Structured;
                        itemsParameter.TypeName = "dbo.NovaRetailSaleItemTVP";

                        var tendersParameter = cmd.Parameters.AddWithValue("@Tenders", NovaRetailSalesSqlMapper.ToTendersTable(request.Tenders));
                        tendersParameter.SqlDbType = SqlDbType.Structured;
                        tendersParameter.TypeName = "dbo.NovaRetailSaleTenderTVP";

                        perfConnection.Restart();
                        var transactionNumberParameter = new SqlParameter("@TransactionNumber", SqlDbType.Int)
                        {
                            Direction = ParameterDirection.Output
                        };
                        cmd.Parameters.Add(transactionNumberParameter);

                        using (var reader = cmd.ExecuteReader())
                        {
                            if (reader.Read())
                            {
                                response.Ok = NovaRetailSalesSqlMapper.GetBoolean(reader, "Ok");
                                response.Message = NovaRetailSalesSqlMapper.GetString(reader, "Message", NovaRetailSalesSqlMapper.GetString(reader, "ErrorMessage", string.Empty));
                                response.TransactionNumber = NovaRetailSalesSqlMapper.GetInt(reader, "TransactionNumber");
                                response.BatchNumber = NovaRetailSalesSqlMapper.GetNullableInt(reader, "BatchNumber");
                                response.SubTotal = NovaRetailSalesSqlMapper.GetNullableDecimal(reader, "SubTotal");
                                response.Discounts = NovaRetailSalesSqlMapper.GetNullableDecimal(reader, "Discounts");
                                response.SalesTax = NovaRetailSalesSqlMapper.GetNullableDecimal(reader, "SalesTax");
                                response.Total = NovaRetailSalesSqlMapper.GetNullableDecimal(reader, "Total");
                                response.TenderTotal = NovaRetailSalesSqlMapper.GetNullableDecimal(reader, "TenderTotal");
                                response.ErrorNumber = NovaRetailSalesSqlMapper.GetNullableInt(reader, "ErrorNumber");
                                response.ErrorProcedure = NovaRetailSalesSqlMapper.GetString(reader, "ErrorProcedure", string.Empty);
                                response.ErrorLine = NovaRetailSalesSqlMapper.GetNullableInt(reader, "ErrorLine");
                            }
                        }

                        if (response.TransactionNumber <= 0 && transactionNumberParameter.Value != DBNull.Value)
                        {
                            response.TransactionNumber = Convert.ToInt32(transactionNumberParameter.Value);
                        }

                        LogPerformance($"CreateSale spNovaRetail_CreateSale {perfConnection.ElapsedMilliseconds} ms ok={response.Ok} tn={response.TransactionNumber}");
                    }

                    if (saleTransaction != null)
                    {
                        if (response.Ok)
                            saleTransaction.Commit();
                        else
                            SafeRollback(saleTransaction);

                        saleTransaction = null;
                    }

                    if (response.Ok && response.TransactionNumber > 0)
                    {
                        perfConnection.Restart();
                        response.BatchNumber = EnsureTransactionBatchNumber(cn, response.TransactionNumber, request.StoreID, request.RegisterID, response.BatchNumber ?? activeBatch.BatchNumber);
                        LogPerformance($"CreateSale EnsureTransactionBatchNumber {perfConnection.ElapsedMilliseconds} ms");

                        try
                        {
                            perfConnection.Restart();
                            SyncPersistedTransactionEntryValues(cn, response.TransactionNumber, request);
                            LogPerformance($"CreateSale SyncPersistedTransactionEntryValues {perfConnection.ElapsedMilliseconds} ms");
                        }
                        catch (Exception exEntries)
                        {
                            response.Warnings.Add($"TransactionEntry: {exEntries.Message}");
                        }

                        try
                        {
                            perfConnection.Restart();
                            var correctedTotals = SyncPersistedTransactionTotals(cn, response.TransactionNumber, request);
                            LogPerformance($"CreateSale SyncPersistedTransactionTotals {perfConnection.ElapsedMilliseconds} ms");
                            if (correctedTotals.Total > 0m)
                            {
                                response.SubTotal = correctedTotals.SubTotal;
                                response.Discounts = correctedTotals.Discounts;
                                response.SalesTax = correctedTotals.SalesTax;
                                response.Total = correctedTotals.Total;
                            }
                        }
                        catch (Exception exTotals)
                        {
                            response.Warnings.Add($"Totals: {exTotals.Message}");
                        }

                        try
                        {
                            perfConnection.Restart();
                            response.TaxEntriesInserted = EnsureTaxEntries(cn, request, response.TransactionNumber, request.StoreID);
                            LogPerformance($"CreateSale EnsureTaxEntries {perfConnection.ElapsedMilliseconds} ms inserted={response.TaxEntriesInserted}");
                        }
                        catch (Exception exTax)
                        {
                            response.Warnings.Add($"TaxEntry: {exTax.Message}");
                        }

                        if (RequiresFiscalDocument(request))
                        {
                            try
                            {
                                perfConnection.Restart();
                                EnsureFiscalArtifacts(cn, request, response.TransactionNumber);
                                LogPerformance($"CreateSale EnsureFiscalArtifacts {perfConnection.ElapsedMilliseconds} ms");
                                response.Clave50 = request.CLAVE50 ?? string.Empty;
                                response.Clave20 = request.CLAVE20 ?? string.Empty;
                                response.TiqueteEsperaOk = true;
                            }
                            catch (Exception exTiquete)
                            {
                                response.TiqueteEsperaOk = false;
                                response.Warnings.Add($"TiqueteEspera: {exTiquete.Message}");
                                LogError(new InvalidOperationException($"Fallo fiscal para transaccion {response.TransactionNumber}.", exTiquete));
                            }

                            if (request.Exonera == 1)
                            {
                                try
                                {
                                    perfConnection.Restart();
                                    EnsureExonerationEntries(cn, request, response.TransactionNumber);
                                    LogPerformance($"CreateSale EnsureExonerationEntries {perfConnection.ElapsedMilliseconds} ms");
                                }
                                catch (Exception exExon)
                                {
                                    response.Warnings.Add($"Exoneracion: {exExon.Message}");
                                }
                            }
                        }

                        if (request.RecallType == HoldRecallType && request.RecallID > 0)
                        {
                            try
                            {
                                DeleteTransactionHold(cn, request.RecallID);
                            }
                            catch (Exception exHold)
                            {
                                response.Warnings.Add($"DeleteHold: {exHold.Message}");
                            }
                        }
                        else if (request.RecallType == QuoteRecallType && request.RecallID > 0)
                        {
                            try
                            {
                                var orderType = LoadOrderType(cn, request.RecallID);
                                var rowsClosed = orderType == WorkOrderType
                                    ? CloseWorkOrder(cn, request.RecallID)
                                    : CloseQuoteOrder(cn, request.RecallID);

                                if (rowsClosed == 0)
                                {
                                    var orderLabel = orderType == WorkOrderType ? "Orden de trabajo" : "CotizaciÃƒÆ’Ã‚Â³n";
                                    response.Warnings.Add($"CloseQuote: {orderLabel} #{request.RecallID} no encontrada o ya cerrada.");
                                }
                                else if (orderType != WorkOrderType)
                                {
                                    NovaRetailAuditLogger.Log(
                                        cn,
                                        "QuoteConverted",
                                        "Quote",
                                        request.RecallID,
                                        request.CashierID,
                                        request.StoreID,
                                        request.RegisterID,
                                        response.Total ?? request.Items.Sum(i => i.UnitPrice * i.Quantity),
                                        $"Cotizacion #{request.RecallID} convertida en venta #{response.TransactionNumber}");
                                }
                            }
                            catch (Exception exQuote)
                            {
                                response.Warnings.Add($"CloseQuote: {exQuote.Message}");
                            }
                        }

                        // ÃƒÂ¢Ã¢â‚¬ÂÃ¢â€šÂ¬ÃƒÂ¢Ã¢â‚¬ÂÃ¢â€šÂ¬ AR Transaction: create entry for credit sales / credit NCs ÃƒÂ¢Ã¢â‚¬ÂÃ¢â€šÂ¬ÃƒÂ¢Ã¢â‚¬ÂÃ¢â€šÂ¬
                        NovaRetailAuditLogger.Log(
                            cn,
                            "SaleCreated",
                            "Sale",
                            response.TransactionNumber,
                            request.CashierID,
                            request.StoreID,
                            request.RegisterID,
                            response.Total ?? request.Items.Sum(i => i.UnitPrice * i.Quantity),
                            $"Venta #{response.TransactionNumber} registrada");

                        perfConnection.Restart();
                        LogSaleItemAudit(cn, request, response.TransactionNumber);
                        LogPerformance($"CreateSale LogSaleItemAudit {perfConnection.ElapsedMilliseconds} ms");

                        try
                        {
                            perfConnection.Restart();
                            TryCreateARTransaction(request, response);
                            LogPerformance($"CreateSale TryCreateARTransaction {perfConnection.ElapsedMilliseconds} ms created={response.AccountsReceivableEntryCreated} applied={response.AccountsReceivableApplied} amount={response.AccountsReceivableAppliedAmount:N2}");
                        }
                        catch (Exception exAR)
                        {
                            response.Warnings.Add($"AR_Transaction: {exAR.Message}");
                            LogError(new InvalidOperationException($"Fallo AR para transaccion {response.TransactionNumber}.", exAR));
                        }
                    }
                }

                if (!response.Ok && string.IsNullOrWhiteSpace(response.Message))
                {
                    response.Message = "No fue posible registrar la venta.";
                }

                LogPerformance($"CreateSale finished total={perfRequestStarted.ElapsedMilliseconds} ms ok={response.Ok} tn={response.TransactionNumber}");

                return Request.CreateResponse(response.Ok ? HttpStatusCode.OK : HttpStatusCode.BadRequest, response);
            }
            catch (SqlException ex)
            {
                SafeRollback(saleTransaction);
                saleTransaction = null;

                response.Ok = false;
                response.Message = "Error de base de datos al procesar la venta.";
                response.ErrorNumber = ex.Number;
                LogError(ex);

                return Request.CreateResponse(HttpStatusCode.BadRequest, response);
            }
            catch (Exception ex)
            {
                SafeRollback(saleTransaction);
                saleTransaction = null;

                response.Ok = false;
                response.Message = "Error interno al procesar la venta.";

                try { System.IO.File.AppendAllText(
                    System.IO.Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "nova_error.log"),
                    $"[{DateTime.Now:yyyy-MM-dd HH:mm:ss}] {ex}\r\n\r\n"); } catch { }

                return Request.CreateResponse(HttpStatusCode.InternalServerError, response);
            }
        }

        private static HashSet<int> LoadNonInventoryItemTypes(SqlConnection cn, SqlTransaction tx = null)
        {
            using (var cmd = new SqlCommand("SELECT TOP 1 LTRIM(RTRIM(VALOR)) FROM dbo.AVS_Parametros WHERE CODIGO = 'IT-01'", cn))
            {
                if (tx != null)
                    cmd.Transaction = tx;

                var value = cmd.ExecuteScalar();
                return ParseItemTypes(value == null || value == DBNull.Value ? string.Empty : Convert.ToString(value));
            }
        }

        private static HashSet<int> ParseItemTypes(string value)
        {
            var itemTypes = new HashSet<int>();
            if (string.IsNullOrWhiteSpace(value))
                return itemTypes;

            foreach (var part in value.Split(new[] { ',' }, StringSplitOptions.RemoveEmptyEntries))
            {
                int itemType;
                if (int.TryParse(part.Trim(), out itemType))
                    itemTypes.Add(itemType);
            }

            return itemTypes;
        }

        private static bool RequestContainsNonInventoryItems(IEnumerable<NovaRetailSaleItemDto> items, ISet<int> nonInventoryItemTypes)
        {
            if (items == null || nonInventoryItemTypes == null || nonInventoryItemTypes.Count == 0)
                return false;

            return items.Any(item => item != null && nonInventoryItemTypes.Contains(item.ItemType));
        }

        private static InventoryValidationResult ValidateInventoryItems(SqlConnection cn, SqlTransaction tx, IEnumerable<NovaRetailSaleItemDto> items, ISet<int> nonInventoryItemTypes)
        {
            var result = new InventoryValidationResult { StockOk = true };
            if (items == null)
                return result;

            var requestedQuantities = items
                .Where(item => item != null && item.ItemID > 0 && item.Quantity > 0)
                .GroupBy(item => item.ItemID)
                .Select(group => new RequestedItemQuantity
                {
                    ItemID = group.Key,
                    Quantity = group.Sum(item => item.Quantity)
                })
                .ToList();

            if (requestedQuantities.Count == 0)
                return result;

            var inventorySnapshot = LoadItemInventorySnapshot(cn, tx, requestedQuantities.Select(item => item.ItemID));
            foreach (var requested in requestedQuantities)
            {
                ItemInventorySnapshot snapshot;
                if (!inventorySnapshot.TryGetValue(requested.ItemID, out snapshot))
                    continue;

                if (nonInventoryItemTypes.Contains(snapshot.ItemType))
                {
                    result.HasNonInventoryItems = true;
                    continue;
                }

                if (snapshot.Quantity < requested.Quantity)
                {
                    result.StockOk = false;
                    return result;
                }
            }

            return result;
        }

        private static Dictionary<int, ItemInventorySnapshot> LoadItemInventorySnapshot(SqlConnection cn, SqlTransaction tx, IEnumerable<int> itemIds)
        {
            var ids = itemIds == null
                ? new List<int>()
                : itemIds.Where(id => id > 0).Distinct().ToList();

            var snapshot = new Dictionary<int, ItemInventorySnapshot>();
            if (ids.Count == 0)
                return snapshot;

            using (var cmd = new SqlCommand())
            {
                cmd.Connection = cn;
                cmd.Transaction = tx;
                cmd.CommandTimeout = 30;

                var parameterNames = new List<string>(ids.Count);
                for (var index = 0; index < ids.Count; index++)
                {
                    var parameterName = "@ItemID" + index.ToString(CultureInfo.InvariantCulture);
                    parameterNames.Add(parameterName);
                    cmd.Parameters.AddWithValue(parameterName, ids[index]);
                }

                cmd.CommandText = @"SELECT I.ID,
                                           CAST(ISNULL(I.Quantity, 0) AS decimal(18, 4)) AS Quantity,
                                           ISNULL(I.ItemType, 0) AS ItemType
                                    FROM dbo.Item I WITH (UPDLOCK, HOLDLOCK)
                                    WHERE I.ID IN (" + string.Join(", ", parameterNames) + ")";

                using (var reader = cmd.ExecuteReader())
                {
                    while (reader.Read())
                    {
                        snapshot[Convert.ToInt32(reader["ID"])] = new ItemInventorySnapshot
                        {
                            Quantity = reader["Quantity"] == DBNull.Value ? 0m : Convert.ToDecimal(reader["Quantity"]),
                            ItemType = reader["ItemType"] == DBNull.Value ? 0 : Convert.ToInt32(reader["ItemType"])
                        };
                    }
                }
            }

            return snapshot;
        }

        private static void SafeRollback(SqlTransaction tx)
        {
            if (tx == null)
                return;

            try
            {
                tx.Rollback();
            }
            catch
            {
            }
        }

        private static ActiveBatchInfo ResolveActiveBatch(SqlConnection cn, int requestedStoreId, int requestedRegisterId, SqlTransaction tx = null)
        {
            var candidates = new List<Tuple<string, SqlParameter[]>>();

            if (requestedStoreId > 0 && requestedRegisterId > 0)
            {
                candidates.Add(Tuple.Create(
                    @"SELECT TOP 1 BatchNumber, StoreID, RegisterID
                      FROM dbo.Batch
                      WHERE StoreID = @StoreID
                        AND RegisterID = @RegisterID
                        AND ClosingTime IS NULL
                        AND Status IN (0, 2, 4, 6)
                      ORDER BY OpeningTime DESC, BatchNumber DESC",
                    new[]
                    {
                        new SqlParameter("@StoreID", requestedStoreId),
                        new SqlParameter("@RegisterID", requestedRegisterId)
                    }));
            }

            if (requestedStoreId > 0)
            {
                candidates.Add(Tuple.Create(
                    @"SELECT TOP 1 BatchNumber, StoreID, RegisterID
                      FROM dbo.Batch
                      WHERE StoreID = @StoreID
                        AND ClosingTime IS NULL
                        AND Status IN (0, 2, 4, 6)
                      ORDER BY OpeningTime DESC, BatchNumber DESC",
                    new[] { new SqlParameter("@StoreID", requestedStoreId) }));
            }

            candidates.Add(Tuple.Create(
                @"SELECT TOP 1 BatchNumber, StoreID, RegisterID
                  FROM dbo.Batch
                  WHERE ClosingTime IS NULL
                    AND Status IN (0, 2, 4, 6)
                  ORDER BY OpeningTime DESC, BatchNumber DESC",
                Array.Empty<SqlParameter>()));

            foreach (var candidate in candidates)
            {
                using (var cmd = new SqlCommand(candidate.Item1, cn))
                {
                    if (tx != null)
                        cmd.Transaction = tx;
                    if (candidate.Item2.Length > 0)
                        cmd.Parameters.AddRange(candidate.Item2);

                    using (var reader = cmd.ExecuteReader())
                    {
                        if (reader.Read())
                        {
                            return new ActiveBatchInfo
                            {
                                BatchNumber = reader["BatchNumber"] != DBNull.Value ? Convert.ToInt32(reader["BatchNumber"]) : 0,
                                StoreID = reader["StoreID"] != DBNull.Value ? Convert.ToInt32(reader["StoreID"]) : requestedStoreId,
                                RegisterID = reader["RegisterID"] != DBNull.Value ? Convert.ToInt32(reader["RegisterID"]) : requestedRegisterId
                            };
                        }
                    }
                }
            }

            return null;
        }

        private static int EnsureTransactionBatchNumber(SqlConnection cn, int transactionNumber, int storeId, int registerId, int fallbackBatchNumber)
        {
            using (var cmd = new SqlCommand("SELECT BatchNumber FROM dbo.[Transaction] WHERE TransactionNumber = @TransactionNumber", cn))
            {
                cmd.Parameters.AddWithValue("@TransactionNumber", transactionNumber);
                var currentBatchValue = cmd.ExecuteScalar();
                if (currentBatchValue != null && currentBatchValue != DBNull.Value)
                {
                    var currentBatch = Convert.ToInt32(currentBatchValue);
                    if (currentBatch > 0)
                        return currentBatch;
                }
            }

            var activeBatch = ResolveActiveBatch(cn, storeId, registerId);
            var batchNumber = activeBatch != null && activeBatch.BatchNumber > 0
                ? activeBatch.BatchNumber
                : fallbackBatchNumber;

            if (batchNumber > 0)
            {
                using (var cmd = new SqlCommand("UPDATE dbo.[Transaction] SET BatchNumber = @BatchNumber WHERE TransactionNumber = @TransactionNumber AND ISNULL(BatchNumber, 0) = 0", cn))
                {
                    cmd.Parameters.AddWithValue("@BatchNumber", batchNumber);
                    cmd.Parameters.AddWithValue("@TransactionNumber", transactionNumber);
                    cmd.ExecuteNonQuery();
                }
            }

            return batchNumber;
        }

        private static void EnsureClaves(NovaRetailCreateSaleRequest request, int transactionNumber, SqlConnection cn)
        {
            if (!string.IsNullOrWhiteSpace(request.CLAVE50) && !string.IsNullOrWhiteSpace(request.CLAVE20))
                return;

            // Leer cÃƒÆ’Ã‚Â©dula del emisor ÃƒÂ¢Ã¢â€šÂ¬Ã¢â‚¬Â intentar varias tablas posibles
            var cedulaEmisor = string.Empty;
            var cedulaQueries = new[]
            {
                // VATDetailID almacena la cÃƒÆ’Ã‚Â©dula jurÃƒÆ’Ã‚Â­dica/fÃƒÆ’Ã‚Â­sica del emisor en RMH Costa Rica
                "SELECT TOP 1 ISNULL(CAST(VATDetailID AS NVARCHAR(20)),'')           FROM dbo.[Configuration]",
                // VATRegistrationNumber puede contener un cÃƒÆ’Ã‚Â³digo interno (ej. "201"), no la cÃƒÆ’Ã‚Â©dula real
                "SELECT TOP 1 ISNULL(CAST(VATRegistrationNumber AS NVARCHAR(20)),'') FROM dbo.[Configuration]",
                // Fallbacks por si la instalaciÃƒÆ’Ã‚Â³n usa otra tabla/columna
                "SELECT TOP 1 ISNULL(CAST(Valor AS NVARCHAR(20)),'') FROM dbo.AVS_Parametros WHERE UPPER(Nombre) LIKE '%CEDULA%' OR UPPER(Nombre) LIKE '%NIF%'",
                "SELECT TOP 1 ISNULL(CAST(TaxNumber AS NVARCHAR(20)),'')     FROM dbo.[Configuration]",
                "SELECT TOP 1 ISNULL(CAST(NIF AS NVARCHAR(20)),'')           FROM dbo.AVS_DATOS_EMISOR",
                "SELECT TOP 1 ISNULL(CAST(CedulaEmisor AS NVARCHAR(20)),'')  FROM dbo.AVS_DATOS_EMISOR",
                "SELECT TOP 1 ISNULL(CAST(TaxNumber AS NVARCHAR(20)),'')     FROM dbo.Store WHERE StoreID = 1",
            };

            foreach (var query in cedulaQueries)
            {
                try
                {
                    using (var cmd = new SqlCommand(query, cn))
                    {
                        var val = cmd.ExecuteScalar();
                        if (val != null && val != DBNull.Value)
                        {
                            // Normalizar: quitar guiones y espacios (ej. "3-101-639680" ÃƒÂ¢Ã¢â‚¬Â Ã¢â‚¬â„¢ "3101639680")
                            var candidate = new string(val.ToString().Where(char.IsDigit).ToArray());
                            if (!string.IsNullOrWhiteSpace(candidate) && candidate != "0")
                            {
                                cedulaEmisor = candidate;
                                break;
                            }
                        }
                    }
                }
                catch { /* tabla no existe, probar la siguiente */ }
            }

            var date      = DateTime.Now;
            var ddmmaa    = date.ToString("ddMMyy");
            var cedulaPad = cedulaEmisor.PadLeft(12, '0');
            var sucursal  = (request.COD_SUCURSAL ?? "001").PadLeft(3, '0');
            var terminal  = (request.TERMINAL_POS ?? "00001").PadLeft(5, '0');
            var tipoCvta  = string.IsNullOrWhiteSpace(request.COMPROBANTE_TIPO) ? "04" : request.COMPROBANTE_TIPO.PadLeft(2, '0');
            // Usar COMPROBANTE_INTERNO (consecutivo de AVS_INTEGRAFAST_02) si estÃƒÆ’Ã‚Â¡ disponible
            var consec    = (!string.IsNullOrWhiteSpace(request.COMPROBANTE_INTERNO)
                ? request.COMPROBANTE_INTERNO : transactionNumber.ToString()).PadLeft(10, '0');
            var situacion = "1";   // Normal
            var seguridad = new Random().Next(10000000, 99999999).ToString("D8");

            if (string.IsNullOrWhiteSpace(request.CLAVE20))
                request.CLAVE20 = sucursal + terminal + tipoCvta + consec;

            if (string.IsNullOrWhiteSpace(request.CLAVE50))
                request.CLAVE50 = "506" + ddmmaa + cedulaPad + sucursal + terminal + tipoCvta + consec + situacion + seguridad;

            if (string.IsNullOrWhiteSpace(request.COMPROBANTE_SITUACION))
                request.COMPROBANTE_SITUACION = situacion;
        }

        private static int EnsureTaxEntries(SqlConnection cn, NovaRetailCreateSaleRequest request, int transactionNumber, int storeId)
        {
            if (request.Items == null || request.Items.Count == 0)
                return 0;

            // Cargar TransactionEntry: indexar por DetailID y por posiciÃƒÆ’Ã‚Â³n secuencial
            if (UseSetBasedSalePostProcessing())
                return EnsureTaxEntriesSetBased(cn, request, transactionNumber, storeId);

            var entriesByDetailId = new Dictionary<int, int>();
            var entriesOrdered    = new List<(int EntryId, int DetailId, int ItemId)>();

            using (var cmd = new SqlCommand(
                "SELECT ID, ISNULL(DetailID,-1) AS DetailID, ISNULL(ItemID,0) AS ItemID " +
                "FROM dbo.TransactionEntry WHERE TransactionNumber = @TransactionNumber ORDER BY ID", cn))
            {
                cmd.Parameters.AddWithValue("@TransactionNumber", transactionNumber);
                using (var reader = cmd.ExecuteReader())
                {
                    while (reader.Read())
                    {
                        var entryId  = Convert.ToInt32(reader["ID"]);
                        var detailId = Convert.ToInt32(reader["DetailID"]);
                        var itemId   = Convert.ToInt32(reader["ItemID"]);
                        entriesOrdered.Add((entryId, detailId, itemId));
                        if (detailId >= 0 && !entriesByDetailId.ContainsKey(detailId))
                            entriesByDetailId[detailId] = entryId;
                    }
                }
            }

            var inserted = 0;

            foreach (var item in request.Items.OrderBy(i => i.RowNo))
            {
                if (!item.Taxable || !item.TaxID.HasValue)
                    continue;

                // 1) Match por DetailID = RowNo - 1 (asignaciÃƒÆ’Ã‚Â³n estÃƒÆ’Ã‚Â¡ndar del SP)
                var detailId = item.RowNo - 1;
                int transactionEntryId = 0;

                if (entriesByDetailId.TryGetValue(detailId, out var byDetailId) && byDetailId > 0)
                {
                    transactionEntryId = byDetailId;
                }
                else
                {
                    // 2) Fallback: posiciÃƒÆ’Ã‚Â³n secuencial (ÃƒÆ’Ã‚Â­ndice RowNo-1 en la lista)
                    var idx = item.RowNo - 1;
                    if (idx >= 0 && idx < entriesOrdered.Count)
                        transactionEntryId = entriesOrdered[idx].EntryId;

                    // 3) Fallback: primer TransactionEntry con mismo ItemID
                    if (transactionEntryId <= 0)
                    {
                        var byItem = entriesOrdered.FirstOrDefault(e => e.ItemId == item.ItemID);
                        transactionEntryId = byItem.EntryId;
                    }
                }

                if (transactionEntryId <= 0)
                    continue;

                using (var existsCmd = new SqlCommand(
                    "SELECT COUNT(1) FROM dbo.TaxEntry WHERE TransactionEntryID = @TransactionEntryID", cn))
                {
                    existsCmd.Parameters.AddWithValue("@TransactionEntryID", transactionEntryId);
                    if (Convert.ToInt32(existsCmd.ExecuteScalar()) > 0)
                        continue;
                }

                var lineAmount    = Math.Round(item.UnitPrice * item.Quantity, 4, MidpointRounding.AwayFromZero);
                var taxAmount     = Math.Round(item.SalesTax,  4, MidpointRounding.AwayFromZero);
                var taxableAmount = Math.Max(0m, lineAmount);

                using (var insertCmd = new SqlCommand(
                    @"INSERT INTO dbo.TaxEntry (StoreID, TaxID, TransactionNumber, Tax, TaxableAmount, TransactionEntryID, SyncGuid)
                      VALUES (@StoreID, @TaxID, @TransactionNumber, @Tax, @TaxableAmount, @TransactionEntryID, NEWID())", cn))
                {
                    insertCmd.Parameters.AddWithValue("@StoreID",            storeId);
                    insertCmd.Parameters.AddWithValue("@TaxID",              item.TaxID.Value);
                    insertCmd.Parameters.AddWithValue("@TransactionNumber",   transactionNumber);
                    insertCmd.Parameters.AddWithValue("@Tax",                taxAmount);
                    insertCmd.Parameters.AddWithValue("@TaxableAmount",      taxableAmount);
                    insertCmd.Parameters.AddWithValue("@TransactionEntryID", transactionEntryId);
                    insertCmd.ExecuteNonQuery();
                    inserted++;
                }
            }

            return inserted;
        }

        private static bool UseSetBasedSalePostProcessing()
        {
            return true;
        }

        private static int EnsureTaxEntriesSetBased(SqlConnection cn, NovaRetailCreateSaleRequest request, int transactionNumber, int storeId)
        {
            using (var cmd = new SqlCommand(@"
;WITH TaxItems AS
(
    SELECT
        RowNo,
        ItemID,
        TaxID,
        UnitPrice,
        Quantity,
        SalesTax
    FROM @Items
    WHERE Taxable = 1
      AND ISNULL(TaxID, 0) > 0
),
DirectMatches AS
(
    SELECT
        I.RowNo,
        I.ItemID,
        I.TaxID,
        I.UnitPrice,
        I.Quantity,
        I.SalesTax,
        TE.ID AS TransactionEntryID
    FROM TaxItems I
    INNER JOIN dbo.TransactionEntry TE
        ON TE.TransactionNumber = @TransactionNumber
       AND TE.DetailID = I.RowNo - 1
),
MissingItems AS
(
    SELECT I.*
    FROM TaxItems I
    WHERE NOT EXISTS
    (
        SELECT 1
        FROM DirectMatches M
        WHERE M.RowNo = I.RowNo
    )
),
EntriesByRow AS
(
    SELECT
        TE.ID,
        ISNULL(TE.ItemID, 0) AS ItemID,
        ROW_NUMBER() OVER (ORDER BY TE.ID) AS EntryRowNo
    FROM dbo.TransactionEntry TE
    WHERE TE.TransactionNumber = @TransactionNumber
),
FallbackMatches AS
(
    SELECT
        I.RowNo,
        I.ItemID,
        I.TaxID,
        I.UnitPrice,
        I.Quantity,
        I.SalesTax,
        COALESCE(ByRow.ID, ByItem.ID) AS TransactionEntryID
    FROM MissingItems I
    OUTER APPLY
    (
        SELECT TOP (1) E.ID
        FROM EntriesByRow E
        WHERE E.EntryRowNo = I.RowNo
        ORDER BY E.ID
    ) ByRow
    OUTER APPLY
    (
        SELECT TOP (1) E.ID
        FROM EntriesByRow E
        WHERE E.ItemID = I.ItemID
        ORDER BY E.ID
    ) ByItem
),
MatchedItems AS
(
    SELECT RowNo, ItemID, TaxID, UnitPrice, Quantity, SalesTax, TransactionEntryID
    FROM DirectMatches

    UNION ALL

    SELECT RowNo, ItemID, TaxID, UnitPrice, Quantity, SalesTax, TransactionEntryID
    FROM FallbackMatches
    WHERE TransactionEntryID IS NOT NULL
)
INSERT INTO dbo.TaxEntry
    (StoreID, TaxID, TransactionNumber, Tax, TaxableAmount, TransactionEntryID, SyncGuid)
SELECT
    @StoreID,
    I.TaxID,
    @TransactionNumber,
    ROUND(ISNULL(I.SalesTax, 0), 4),
    CASE
        WHEN ROUND(ISNULL(I.UnitPrice, 0) * ISNULL(I.Quantity, 0), 4) < 0 THEN 0
        ELSE ROUND(ISNULL(I.UnitPrice, 0) * ISNULL(I.Quantity, 0), 4)
    END,
    I.TransactionEntryID,
    NEWID()
FROM MatchedItems I
WHERE NOT EXISTS
  (
      SELECT 1
      FROM dbo.TaxEntry Existing
      WHERE Existing.TransactionEntryID = I.TransactionEntryID
  );

SELECT @@ROWCOUNT;", cn))
            {
                cmd.CommandTimeout = 30;
                cmd.Parameters.AddWithValue("@StoreID", storeId);
                cmd.Parameters.AddWithValue("@TransactionNumber", transactionNumber);

                var itemsParameter = cmd.Parameters.AddWithValue("@Items", NovaRetailSalesSqlMapper.ToItemsTable(request.Items));
                itemsParameter.SqlDbType = SqlDbType.Structured;
                itemsParameter.TypeName = "dbo.NovaRetailSaleItemTVP";

                var inserted = cmd.ExecuteScalar();
                return inserted == null || inserted == DBNull.Value ? 0 : Convert.ToInt32(inserted);
            }
        }

        private sealed class ActiveBatchInfo
        {
            public int BatchNumber { get; set; }
            public int StoreID { get; set; }
            public int RegisterID { get; set; }
        }

        private sealed class RequestedItemQuantity
        {
            public int ItemID { get; set; }
            public decimal Quantity { get; set; }
        }

        private sealed class ItemInventorySnapshot
        {
            public decimal Quantity { get; set; }
            public int ItemType { get; set; }
        }

        private sealed class InventoryValidationResult
        {
            public bool StockOk { get; set; }
            public bool HasNonInventoryItems { get; set; }
        }


    }
}
