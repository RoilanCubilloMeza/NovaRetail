using System;
using System.Collections.Generic;
using System.Configuration;
using System.Data;
using System.Data.SqlClient;
using System.Globalization;
using System.Linq;
using System.Net;
using System.Net.Http;
using System.Web.Http;
using System.Xml.Linq;
using NovaAPI.Models;

namespace NovaAPI.Controllers
{
    [RoutePrefix("api/NovaRetailSales")]
    public class NovaRetailSalesController : ApiController
    {
        private const int ReferenceNumberMaxLength = 50;
        private const int HoldRecallType = 1;
        private const int QuoteRecallType = 3;
        private const int WorkOrderType = 2;
        private const int QuoteOrderType = 3;

        private static readonly string ErrorLogPath =
            System.IO.Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "nova_error.log");

        private static void LogError(Exception ex)
        {
            try
            {
                System.IO.File.AppendAllText(ErrorLogPath,
                    $"[{DateTime.Now:yyyy-MM-dd HH:mm:ss}] {ex}\r\n\r\n");
            }
            catch { }
        }

        private static string GetConnectionString()
        {
            var connectionString = AppConfig.ConnectionString("RMHPOS");
            if (string.IsNullOrWhiteSpace(connectionString))
                throw new ConfigurationErrorsException("No se encontró la cadena de conexión RMHPOS para registrar ventas.");

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

        private static string SanitizeReferenceNumber(string referenceNumber)
        {
            if (string.IsNullOrWhiteSpace(referenceNumber))
                return string.Empty;

            var value = referenceNumber.Trim();
            return value.Length <= ReferenceNumberMaxLength
                ? value
                : value.Substring(0, ReferenceNumberMaxLength).TrimEnd();
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
                    Message = "Solicitud inválida."
                });
            }

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
                        ? "Solicitud inválida."
                        : string.Join(" | ", errors)
                });
            }

            if (request.Items == null || request.Items.Count == 0)
            {
                return Request.CreateResponse(HttpStatusCode.BadRequest, new NovaRetailCreateSaleResponse
                {
                    Ok = false,
                    Message = "La venta no contiene ítems."
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
                    cn.Open();

                    var nonInventoryItemTypes = LoadNonInventoryItemTypes(cn);
                    var requiresNonInventoryBypass = !request.AllowNegativeInventory
                        && nonInventoryItemTypes.Count > 0
                        && RequestContainsNonInventoryItems(request.Items, nonInventoryItemTypes);

                    if (requiresNonInventoryBypass)
                    {
                        saleTransaction = cn.BeginTransaction(IsolationLevel.Serializable);

                        var stockValidation = ValidateInventoryItems(cn, saleTransaction, request.Items, nonInventoryItemTypes);
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

                    var activeBatch = ResolveActiveBatch(cn, request.StoreID, request.RegisterID, saleTransaction);
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

                        var itemsParameter = cmd.Parameters.AddWithValue("@Items", ToItemsTable(request.Items));
                        itemsParameter.SqlDbType = SqlDbType.Structured;
                        itemsParameter.TypeName = "dbo.NovaRetailSaleItemTVP";

                        var tendersParameter = cmd.Parameters.AddWithValue("@Tenders", ToTendersTable(request.Tenders));
                        tendersParameter.SqlDbType = SqlDbType.Structured;
                        tendersParameter.TypeName = "dbo.NovaRetailSaleTenderTVP";

                        var transactionNumberParameter = new SqlParameter("@TransactionNumber", SqlDbType.Int)
                        {
                            Direction = ParameterDirection.Output
                        };
                        cmd.Parameters.Add(transactionNumberParameter);

                        using (var reader = cmd.ExecuteReader())
                        {
                            if (reader.Read())
                            {
                                response.Ok = GetBoolean(reader, "Ok");
                                response.Message = GetString(reader, "Message", GetString(reader, "ErrorMessage", string.Empty));
                                response.TransactionNumber = GetInt(reader, "TransactionNumber");
                                response.BatchNumber = GetNullableInt(reader, "BatchNumber");
                                response.SubTotal = GetNullableDecimal(reader, "SubTotal");
                                response.Discounts = GetNullableDecimal(reader, "Discounts");
                                response.SalesTax = GetNullableDecimal(reader, "SalesTax");
                                response.Total = GetNullableDecimal(reader, "Total");
                                response.TenderTotal = GetNullableDecimal(reader, "TenderTotal");
                                response.ErrorNumber = GetNullableInt(reader, "ErrorNumber");
                                response.ErrorProcedure = GetString(reader, "ErrorProcedure", string.Empty);
                                response.ErrorLine = GetNullableInt(reader, "ErrorLine");
                            }
                        }

                        if (response.TransactionNumber <= 0 && transactionNumberParameter.Value != DBNull.Value)
                        {
                            response.TransactionNumber = Convert.ToInt32(transactionNumberParameter.Value);
                        }
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
                        response.BatchNumber = EnsureTransactionBatchNumber(cn, response.TransactionNumber, request.StoreID, request.RegisterID, response.BatchNumber ?? activeBatch.BatchNumber);

                        try
                        {
                            response.TaxEntriesInserted = EnsureTaxEntries(cn, request, response.TransactionNumber, request.StoreID);
                        }
                        catch (Exception exTax)
                        {
                            response.Warnings.Add($"TaxEntry: {exTax.Message}");
                        }

                        if (request.InsertarTiqueteEspera)
                        {
                            try
                            {
                                ApplyIntegraFast02Config(cn, request);
                                EnsureClaves(request, response.TransactionNumber, cn);
                                response.Clave50 = request.CLAVE50 ?? string.Empty;
                                response.Clave20 = request.CLAVE20 ?? string.Empty;
                                EnsureTiqueteEspera(cn, request, response.TransactionNumber);
                                response.TiqueteEsperaOk = true;
                            }
                            catch (Exception exTiquete)
                            {
                                response.TiqueteEsperaOk = false;
                                response.Warnings.Add($"TiqueteEspera: {exTiquete.Message}");
                            }

                            try
                            {
                                EnsureIntegraFast05(cn, request, response.TransactionNumber);
                            }
                            catch (Exception exIf05)
                            {
                                response.Warnings.Add($"IntegraFast05: {exIf05.Message}");
                            }

                            if (request.Exonera == 1)
                            {
                                try
                                {
                                    EnsureExonerationEntries(cn, request, response.TransactionNumber);
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
                                    var orderLabel = orderType == WorkOrderType ? "Orden de trabajo" : "Cotización";
                                    response.Warnings.Add($"CloseQuote: {orderLabel} #{request.RecallID} no encontrada o ya cerrada.");
                                }
                            }
                            catch (Exception exQuote)
                            {
                                response.Warnings.Add($"CloseQuote: {exQuote.Message}");
                            }
                        }

                        // ── AR Transaction: create entry for credit sales / credit NCs ──
                        try
                        {
                            TryCreateARTransaction(request, response);
                        }
                        catch (Exception exAR)
                        {
                            response.Warnings.Add($"AR_Transaction: {exAR.Message}");
                        }
                    }
                }

                if (!response.Ok && string.IsNullOrWhiteSpace(response.Message))
                {
                    response.Message = "No fue posible registrar la venta.";
                }

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

        [HttpGet]
        [Route("invoice-history")]
        public HttpResponseMessage InvoiceHistory(string search = "", int top = 200)
        {
            var connectionString = GetConnectionString();
            try
            {
                var entries = new List<NovaRetailInvoiceHistoryEntryDto>();

                using (var cn = new SqlConnection(connectionString))
                {
                    cn.Open();

                    const string sql = @"
SELECT TOP (@Top)
    t.TransactionNumber,
    CAST(t.[Time] AS datetime) AS [Date],
    ISNULL(NULLIF(f.COMPROBANTE_TIPO, ''), '04') AS ComprobanteTipo,
    ISNULL(f.CLAVE50, '') AS Clave50,
    ISNULL(NULLIF(f.CLAVE20, ''), ISNULL(f.COMPROBANTE_INTERNO, '')) AS Consecutivo,
    ISNULL(NULLIF(f.CEDULA_TRIBUTARIA, ''), ISNULL(NULLIF(c.AccountNumber, ''), '')) AS ClientId,
    COALESCE(
        NULLIF(f.NOMBRE_CLIENTE, ''),
        NULLIF(LTRIM(RTRIM(ISNULL(c.FirstName, '') + ' ' + ISNULL(c.LastName, ''))), ''),
        'CLIENTE CONTADO') AS ClientName,
    CAST(0 AS INT) AS RegisterNumber,
    CAST(ISNULL(s.SubtotalColones, ISNULL(t.Total, 0) - ISNULL(t.SalesTax, 0)) AS decimal(18, 2)) AS SubtotalColones,
    CAST(ISNULL(s.DiscountColones, 0) AS decimal(18, 2)) AS DiscountColones,
    CAST(0 AS decimal(18, 2)) AS ExonerationColones,
    CAST(ISNULL(t.SalesTax, 0) AS decimal(18, 2)) AS TaxColones,
    CAST(ISNULL(t.Total, 0) AS decimal(18, 2)) AS TotalColones,
    ISNULL(c.AccountNumber, '') AS CreditAccountNumber
FROM dbo.[Transaction] t
LEFT JOIN dbo.AVS_INTEGRAFAST_01 f ON f.TRANSACTIONNUMBER = CAST(t.TransactionNumber AS NVARCHAR(50))
LEFT JOIN dbo.Customer c ON c.ID = t.CustomerID
OUTER APPLY (
    SELECT
        SUM(CAST(ISNULL(te.FullPrice, te.Price) * ISNULL(te.Quantity, 0) AS decimal(18, 2))) AS SubtotalColones,
        SUM(CAST(CASE
            WHEN ISNULL(te.FullPrice, 0) > ISNULL(te.Price, 0)
                THEN (ISNULL(te.FullPrice, 0) - ISNULL(te.Price, 0)) * ISNULL(te.Quantity, 0)
            ELSE 0
        END AS decimal(18, 2))) AS DiscountColones
    FROM dbo.TransactionEntry te
    WHERE te.TransactionNumber = t.TransactionNumber
) s
WHERE (@Search = ''
    OR CAST(t.TransactionNumber AS NVARCHAR(50)) LIKE '%' + @Search + '%'
    OR ISNULL(f.CEDULA_TRIBUTARIA, '') LIKE '%' + @Search + '%'
    OR ISNULL(f.NOMBRE_CLIENTE, '') LIKE '%' + @Search + '%'
    OR ISNULL(c.AccountNumber, '') LIKE '%' + @Search + '%'
    OR LTRIM(RTRIM(ISNULL(c.FirstName, '') + ' ' + ISNULL(c.LastName, ''))) LIKE '%' + @Search + '%'
    OR ISNULL(f.CLAVE20, '') LIKE '%' + @Search + '%'
    OR ISNULL(f.COMPROBANTE_INTERNO, '') LIKE '%' + @Search + '%'
    OR ISNULL(f.CLAVE50, '') LIKE '%' + @Search + '%')
ORDER BY t.[Time] DESC";

                    using (var cmd = new SqlCommand(sql, cn))
                    {
                        cmd.CommandTimeout = 60;
                        cmd.Parameters.AddWithValue("@Top", top <= 0 ? 200 : top > 500 ? 500 : top);
                        cmd.Parameters.AddWithValue("@Search", search ?? string.Empty);

                        using (var reader = cmd.ExecuteReader())
                        {
                            while (reader.Read())
                            {
                                entries.Add(new NovaRetailInvoiceHistoryEntryDto
                                {
                                    TransactionNumber = Convert.ToInt32(reader["TransactionNumber"]),
                                    Date = Convert.ToDateTime(reader["Date"]),
                                    ComprobanteTipo = reader["ComprobanteTipo"] == DBNull.Value ? string.Empty : Convert.ToString(reader["ComprobanteTipo"]),
                                    Clave50 = reader["Clave50"] == DBNull.Value ? string.Empty : Convert.ToString(reader["Clave50"]),
                                    Consecutivo = reader["Consecutivo"] == DBNull.Value ? string.Empty : Convert.ToString(reader["Consecutivo"]),
                                    ClientId = reader["ClientId"] == DBNull.Value ? string.Empty : Convert.ToString(reader["ClientId"]),
                                    ClientName = reader["ClientName"] == DBNull.Value ? string.Empty : Convert.ToString(reader["ClientName"]),
                                    RegisterNumber = reader["RegisterNumber"] == DBNull.Value ? 0 : Convert.ToInt32(reader["RegisterNumber"]),
                                    SubtotalColones = reader["SubtotalColones"] == DBNull.Value ? 0m : Convert.ToDecimal(reader["SubtotalColones"]),
                                    DiscountColones = reader["DiscountColones"] == DBNull.Value ? 0m : Convert.ToDecimal(reader["DiscountColones"]),
                                    ExonerationColones = reader["ExonerationColones"] == DBNull.Value ? 0m : Convert.ToDecimal(reader["ExonerationColones"]),
                                    TaxColones = reader["TaxColones"] == DBNull.Value ? 0m : Convert.ToDecimal(reader["TaxColones"]),
                                    TotalColones = reader["TotalColones"] == DBNull.Value ? 0m : Convert.ToDecimal(reader["TotalColones"]),
                                    CreditAccountNumber = reader["CreditAccountNumber"] == DBNull.Value ? string.Empty : Convert.ToString(reader["CreditAccountNumber"])
                                });
                            }
                        }
                    }
                }

                return Request.CreateResponse(HttpStatusCode.OK, new NovaRetailInvoiceHistorySearchResponse
                {
                    Ok = true,
                    Entries = entries
                });
            }
            catch (Exception ex)
            {
                LogError(ex);
                return Request.CreateResponse(HttpStatusCode.InternalServerError, new NovaRetailInvoiceHistorySearchResponse
                {
                    Ok = false,
                    Message = "Error interno al consultar historial."
                });
            }
        }

        [HttpGet]
        [Route("invoice-history-detail/{transactionNumber:int}")]
        public HttpResponseMessage InvoiceHistoryDetail(int transactionNumber)
        {
            var connectionString = GetConnectionString();
            try
            {
                NovaRetailInvoiceHistoryEntryDto entry = null;

                using (var cn = new SqlConnection(connectionString))
                {
                    cn.Open();

                    const string headerSql = @"
SELECT TOP 1
    t.TransactionNumber,
    CAST(t.[Time] AS datetime) AS [Date],
    ISNULL(NULLIF(f.COMPROBANTE_TIPO, ''), '04') AS ComprobanteTipo,
    ISNULL(f.CLAVE50, '') AS Clave50,
    ISNULL(NULLIF(f.CLAVE20, ''), ISNULL(f.COMPROBANTE_INTERNO, '')) AS Consecutivo,
    ISNULL(NULLIF(f.CEDULA_TRIBUTARIA, ''), ISNULL(NULLIF(c.AccountNumber, ''), '')) AS ClientId,
    COALESCE(
        NULLIF(f.NOMBRE_CLIENTE, ''),
        NULLIF(LTRIM(RTRIM(ISNULL(c.FirstName, '') + ' ' + ISNULL(c.LastName, ''))), ''),
        'CLIENTE CONTADO') AS ClientName,
    CAST(0 AS INT) AS RegisterNumber,
    CAST(ISNULL(s.SubtotalColones, ISNULL(t.Total, 0) - ISNULL(t.SalesTax, 0)) AS decimal(18, 2)) AS SubtotalColones,
    CAST(ISNULL(s.DiscountColones, 0) AS decimal(18, 2)) AS DiscountColones,
    CAST(0 AS decimal(18, 2)) AS ExonerationColones,
    CAST(ISNULL(t.SalesTax, 0) AS decimal(18, 2)) AS TaxColones,
    CAST(ISNULL(t.Total, 0) AS decimal(18, 2)) AS TotalColones,
    ISNULL(c.AccountNumber, '') AS CreditAccountNumber
FROM dbo.[Transaction] t
LEFT JOIN dbo.AVS_INTEGRAFAST_01 f ON f.TRANSACTIONNUMBER = CAST(t.TransactionNumber AS NVARCHAR(50))
LEFT JOIN dbo.Customer c ON c.ID = t.CustomerID
OUTER APPLY (
    SELECT
        SUM(CAST(ISNULL(te.FullPrice, te.Price) * ISNULL(te.Quantity, 0) AS decimal(18, 2))) AS SubtotalColones,
        SUM(CAST(CASE
            WHEN ISNULL(te.FullPrice, 0) > ISNULL(te.Price, 0)
                THEN (ISNULL(te.FullPrice, 0) - ISNULL(te.Price, 0)) * ISNULL(te.Quantity, 0)
            ELSE 0
        END AS decimal(18, 2))) AS DiscountColones
    FROM dbo.TransactionEntry te
    WHERE te.TransactionNumber = t.TransactionNumber
) s
WHERE t.TransactionNumber = @TransactionNumber";

                    using (var cmd = new SqlCommand(headerSql, cn))
                    {
                        cmd.CommandTimeout = 60;
                        cmd.Parameters.AddWithValue("@TransactionNumber", transactionNumber);

                        using (var reader = cmd.ExecuteReader())
                        {
                            if (reader.Read())
                            {
                                entry = new NovaRetailInvoiceHistoryEntryDto
                                {
                                    TransactionNumber = Convert.ToInt32(reader["TransactionNumber"]),
                                    Date = Convert.ToDateTime(reader["Date"]),
                                    ComprobanteTipo = reader["ComprobanteTipo"] == DBNull.Value ? string.Empty : Convert.ToString(reader["ComprobanteTipo"]),
                                    Clave50 = reader["Clave50"] == DBNull.Value ? string.Empty : Convert.ToString(reader["Clave50"]),
                                    Consecutivo = reader["Consecutivo"] == DBNull.Value ? string.Empty : Convert.ToString(reader["Consecutivo"]),
                                    ClientId = reader["ClientId"] == DBNull.Value ? string.Empty : Convert.ToString(reader["ClientId"]),
                                    ClientName = reader["ClientName"] == DBNull.Value ? string.Empty : Convert.ToString(reader["ClientName"]),
                                    RegisterNumber = reader["RegisterNumber"] == DBNull.Value ? 0 : Convert.ToInt32(reader["RegisterNumber"]),
                                    SubtotalColones = reader["SubtotalColones"] == DBNull.Value ? 0m : Convert.ToDecimal(reader["SubtotalColones"]),
                                    DiscountColones = reader["DiscountColones"] == DBNull.Value ? 0m : Convert.ToDecimal(reader["DiscountColones"]),
                                    ExonerationColones = reader["ExonerationColones"] == DBNull.Value ? 0m : Convert.ToDecimal(reader["ExonerationColones"]),
                                    TaxColones = reader["TaxColones"] == DBNull.Value ? 0m : Convert.ToDecimal(reader["TaxColones"]),
                                    TotalColones = reader["TotalColones"] == DBNull.Value ? 0m : Convert.ToDecimal(reader["TotalColones"]),
                                    CreditAccountNumber = reader["CreditAccountNumber"] == DBNull.Value ? string.Empty : Convert.ToString(reader["CreditAccountNumber"])
                                };
                            }
                        }
                    }

                    if (entry == null)
                    {
                        return Request.CreateResponse(HttpStatusCode.NotFound, new NovaRetailInvoiceHistoryDetailResponse
                        {
                            Ok = false,
                            Message = "Factura no encontrada."
                        });
                    }

                    const string linesSql = @"
SELECT
    ISNULL(te.ItemID, 0) AS ItemID,
    ISNULL(i.TaxID, 0) AS TaxID,
    ISNULL(NULLIF(i.Description, ''), 'Artículo') AS DisplayName,
    ISNULL(NULLIF(i.ItemLookupCode, ''), CAST(te.ItemID AS NVARCHAR(50))) AS Code,
    CAST(ISNULL(te.Quantity, 0) AS decimal(18, 2)) AS Quantity,
    CAST(ISNULL(tax.Percentage, 0) AS decimal(18, 2)) AS TaxPercentage,
    CAST(ISNULL(te.Price, 0) AS decimal(18, 2)) AS UnitPriceColones,
    CAST(ISNULL(te.Price, 0) * ISNULL(te.Quantity, 0) AS decimal(18, 2)) AS LineTotalColones,
    CAST(CASE WHEN ISNULL(te.FullPrice, 0) > ISNULL(te.Price, 0) AND ISNULL(te.FullPrice, 0) > 0
        THEN (((ISNULL(te.FullPrice, 0) - ISNULL(te.Price, 0)) / ISNULL(te.FullPrice, 1)) * 100)
        ELSE 0 END AS decimal(18, 2)) AS DiscountPercent,
    CAST(CASE WHEN ISNULL(te.FullPrice, 0) > ISNULL(te.Price, 0) THEN 1 ELSE 0 END AS bit) AS HasDiscount
FROM dbo.TransactionEntry te
LEFT JOIN dbo.Item i ON i.ID = te.ItemID
LEFT JOIN dbo.Tax tax ON tax.ID = i.TaxID
WHERE te.TransactionNumber = @TransactionNumber
ORDER BY te.ID";

                    using (var cmd = new SqlCommand(linesSql, cn))
                    {
                        cmd.CommandTimeout = 60;
                        cmd.Parameters.AddWithValue("@TransactionNumber", transactionNumber);

                        using (var reader = cmd.ExecuteReader())
                        {
                            while (reader.Read())
                            {
                                entry.Lines.Add(new NovaRetailInvoiceHistoryLineDto
                                {
                                    ItemID = reader["ItemID"] == DBNull.Value ? 0 : Convert.ToInt32(reader["ItemID"]),
                                    TaxID = reader["TaxID"] == DBNull.Value ? 0 : Convert.ToInt32(reader["TaxID"]),
                                    DisplayName = reader["DisplayName"] == DBNull.Value ? string.Empty : Convert.ToString(reader["DisplayName"]),
                                    Code = reader["Code"] == DBNull.Value ? string.Empty : Convert.ToString(reader["Code"]),
                                    Quantity = reader["Quantity"] == DBNull.Value ? 0m : Convert.ToDecimal(reader["Quantity"]),
                                    TaxPercentage = reader["TaxPercentage"] == DBNull.Value ? 0m : Convert.ToDecimal(reader["TaxPercentage"]),
                                    UnitPriceColones = reader["UnitPriceColones"] == DBNull.Value ? 0m : Convert.ToDecimal(reader["UnitPriceColones"]),
                                    LineTotalColones = reader["LineTotalColones"] == DBNull.Value ? 0m : Convert.ToDecimal(reader["LineTotalColones"]),
                                    HasDiscount = reader["HasDiscount"] != DBNull.Value && Convert.ToBoolean(reader["HasDiscount"]),
                                    DiscountPercent = reader["DiscountPercent"] == DBNull.Value ? 0m : Convert.ToDecimal(reader["DiscountPercent"])
                                });
                            }
                        }
                    }
                }

                return Request.CreateResponse(HttpStatusCode.OK, new NovaRetailInvoiceHistoryDetailResponse
                {
                    Ok = true,
                    Entry = entry
                });
            }
            catch (Exception ex)
            {
                LogError(ex);
                return Request.CreateResponse(HttpStatusCode.InternalServerError, new NovaRetailInvoiceHistoryDetailResponse
                {
                    Ok = false,
                    Message = "Error interno al consultar detalle de factura."
                });
            }
        }

        private static DataTable ToItemsTable(IEnumerable<NovaRetailSaleItemDto> items)
        {
            var dt = new DataTable();
            dt.Columns.Add("RowNo", typeof(int));
            dt.Columns.Add("ItemID", typeof(int));
            dt.Columns.Add("Quantity", typeof(decimal));
            dt.Columns.Add("UnitPrice", typeof(decimal));
            dt.Columns.Add("FullPrice", typeof(decimal));
            dt.Columns.Add("Cost", typeof(decimal));
            dt.Columns.Add("Commission", typeof(decimal));
            dt.Columns.Add("PriceSource", typeof(int));
            dt.Columns.Add("SalesRepID", typeof(int));
            dt.Columns.Add("Taxable", typeof(bool));
            dt.Columns.Add("TaxID", typeof(int));
            dt.Columns.Add("SalesTax", typeof(decimal));
            dt.Columns.Add("LineComment", typeof(string));
            dt.Columns.Add("DiscountReasonCodeID", typeof(int));
            dt.Columns.Add("ReturnReasonCodeID", typeof(int));
            dt.Columns.Add("TaxChangeReasonCodeID", typeof(int));
            dt.Columns.Add("QuantityDiscountID", typeof(int));
            dt.Columns.Add("ItemType", typeof(int));
            dt.Columns.Add("ComputedQuantity", typeof(decimal));
            dt.Columns.Add("IsAddMoney", typeof(bool));
            dt.Columns.Add("VoucherID", typeof(int));
            dt.Columns.Add("ExtendedDescription", typeof(string));
            dt.Columns.Add("PromotionID", typeof(int));
            dt.Columns.Add("PromotionName", typeof(string));
            dt.Columns.Add("LineDiscountAmount", typeof(decimal));
            dt.Columns.Add("LineDiscountPercent", typeof(decimal));

            foreach (var item in items)
            {
                dt.Rows.Add(
                    item.RowNo,
                    item.ItemID,
                    item.Quantity,
                    item.UnitPrice,
                    item.FullPrice.HasValue ? (object)item.FullPrice.Value : DBNull.Value,
                    item.Cost,
                    item.Commission,
                    item.PriceSource,
                    item.SalesRepID,
                    item.Taxable,
                    item.TaxID.HasValue ? (object)item.TaxID.Value : DBNull.Value,
                    item.SalesTax,
                    item.LineComment ?? string.Empty,
                    item.DiscountReasonCodeID,
                    item.ReturnReasonCodeID,
                    item.TaxChangeReasonCodeID,
                    item.QuantityDiscountID,
                    item.ItemType,
                    item.ComputedQuantity,
                    item.IsAddMoney,
                    item.VoucherID,
                    string.IsNullOrWhiteSpace(item.ExtendedDescription) ? (object)DBNull.Value : item.ExtendedDescription,
                    item.PromotionID.HasValue ? (object)item.PromotionID.Value : DBNull.Value,
                    string.IsNullOrWhiteSpace(item.PromotionName) ? (object)DBNull.Value : item.PromotionName,
                    item.LineDiscountAmount,
                    item.LineDiscountPercent);
            }

            return dt;
        }

        private static DataTable ToTendersTable(IEnumerable<NovaRetailSaleTenderDto> tenders)
        {
            var dt = new DataTable();
            dt.Columns.Add("RowNo", typeof(int));
            dt.Columns.Add("TenderID", typeof(int));
            dt.Columns.Add("PaymentID", typeof(int));
            dt.Columns.Add("Description", typeof(string));
            dt.Columns.Add("Amount", typeof(decimal));
            dt.Columns.Add("AmountForeign", typeof(decimal));
            dt.Columns.Add("RoundingError", typeof(decimal));
            dt.Columns.Add("CreditCardExpiration", typeof(string));
            dt.Columns.Add("CreditCardNumber", typeof(string));
            dt.Columns.Add("CreditCardApprovalCode", typeof(string));
            dt.Columns.Add("AccountHolder", typeof(string));
            dt.Columns.Add("BankNumber", typeof(string));
            dt.Columns.Add("SerialNumber", typeof(string));
            dt.Columns.Add("State", typeof(string));
            dt.Columns.Add("License", typeof(string));
            dt.Columns.Add("BirthDate", typeof(DateTime));
            dt.Columns.Add("TransitNumber", typeof(string));
            dt.Columns.Add("VisaNetAuthorizationID", typeof(int));
            dt.Columns.Add("DebitSurcharge", typeof(decimal));
            dt.Columns.Add("CashBackSurcharge", typeof(decimal));
            dt.Columns.Add("IsCreateNew", typeof(bool));
            dt.Columns.Add("MedioPagoCodigo", typeof(string));

            foreach (var tender in tenders)
            {
                dt.Rows.Add(
                    tender.RowNo,
                    tender.TenderID,
                    tender.PaymentID,
                    tender.Description ?? string.Empty,
                    tender.Amount,
                    tender.AmountForeign.HasValue ? (object)tender.AmountForeign.Value : DBNull.Value,
                    tender.RoundingError,
                    string.IsNullOrWhiteSpace(tender.CreditCardExpiration) ? (object)DBNull.Value : tender.CreditCardExpiration,
                    string.IsNullOrWhiteSpace(tender.CreditCardNumber) ? (object)DBNull.Value : tender.CreditCardNumber,
                    string.IsNullOrWhiteSpace(tender.CreditCardApprovalCode) ? (object)DBNull.Value : tender.CreditCardApprovalCode,
                    string.IsNullOrWhiteSpace(tender.AccountHolder) ? (object)DBNull.Value : tender.AccountHolder,
                    string.IsNullOrWhiteSpace(tender.BankNumber) ? (object)DBNull.Value : tender.BankNumber,
                    string.IsNullOrWhiteSpace(tender.SerialNumber) ? (object)DBNull.Value : tender.SerialNumber,
                    string.IsNullOrWhiteSpace(tender.State) ? (object)DBNull.Value : tender.State,
                    string.IsNullOrWhiteSpace(tender.License) ? (object)DBNull.Value : tender.License,
                    tender.BirthDate.HasValue ? (object)tender.BirthDate.Value : DBNull.Value,
                    string.IsNullOrWhiteSpace(tender.TransitNumber) ? (object)DBNull.Value : tender.TransitNumber,
                    tender.VisaNetAuthorizationID,
                    tender.DebitSurcharge,
                    tender.CashBackSurcharge,
                    tender.IsCreateNew,
                    string.IsNullOrWhiteSpace(tender.MedioPagoCodigo) ? (object)DBNull.Value : tender.MedioPagoCodigo);
            }

            return dt;
        }

        private static bool HasColumn(IDataRecord reader, string columnName)
        {
            for (var i = 0; i < reader.FieldCount; i++)
            {
                if (string.Equals(reader.GetName(i), columnName, StringComparison.OrdinalIgnoreCase))
                {
                    return true;
                }
            }

            return false;
        }

        private static string GetString(IDataRecord reader, string columnName, string defaultValue)
        {
            if (!HasColumn(reader, columnName))
            {
                return defaultValue;
            }

            var value = reader[columnName];
            return value == DBNull.Value ? defaultValue : Convert.ToString(value);
        }

        private static int GetInt(IDataRecord reader, string columnName)
        {
            if (!HasColumn(reader, columnName))
            {
                return 0;
            }

            var value = reader[columnName];
            return value == DBNull.Value ? 0 : Convert.ToInt32(value);
        }

        private static int? GetNullableInt(IDataRecord reader, string columnName)
        {
            if (!HasColumn(reader, columnName))
            {
                return null;
            }

            var value = reader[columnName];
            return value == DBNull.Value ? (int?)null : Convert.ToInt32(value);
        }

        private static decimal? GetNullableDecimal(IDataRecord reader, string columnName)
        {
            if (!HasColumn(reader, columnName))
            {
                return null;
            }

            var value = reader[columnName];
            return value == DBNull.Value ? (decimal?)null : Convert.ToDecimal(value);
        }

        private static bool GetBoolean(IDataRecord reader, string columnName)
        {
            if (!HasColumn(reader, columnName))
            {
                return false;
            }

            var value = reader[columnName];
            return value != DBNull.Value && Convert.ToBoolean(value);
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

            // Leer cédula del emisor — intentar varias tablas posibles
            var cedulaEmisor = string.Empty;
            var cedulaQueries = new[]
            {
                // VATDetailID almacena la cédula jurídica/física del emisor en RMH Costa Rica
                "SELECT TOP 1 ISNULL(CAST(VATDetailID AS NVARCHAR(20)),'')           FROM dbo.[Configuration]",
                // VATRegistrationNumber puede contener un código interno (ej. "201"), no la cédula real
                "SELECT TOP 1 ISNULL(CAST(VATRegistrationNumber AS NVARCHAR(20)),'') FROM dbo.[Configuration]",
                // Fallbacks por si la instalación usa otra tabla/columna
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
                            // Normalizar: quitar guiones y espacios (ej. "3-101-639680" → "3101639680")
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
            // Usar COMPROBANTE_INTERNO (consecutivo de AVS_INTEGRAFAST_02) si está disponible
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

            var taxSystem = GetTaxSystem(cn);

            // Cargar TransactionEntry: indexar por DetailID y por posición secuencial
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

                // 1) Match por DetailID = RowNo - 1 (asignación estándar del SP)
                var detailId = item.RowNo - 1;
                int transactionEntryId = 0;

                if (entriesByDetailId.TryGetValue(detailId, out var byDetailId) && byDetailId > 0)
                {
                    transactionEntryId = byDetailId;
                }
                else
                {
                    // 2) Fallback: posición secuencial (índice RowNo-1 en la lista)
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
                var taxableAmount = taxSystem > 0
                    ? Math.Max(0m, Math.Round(lineAmount - taxAmount, 4, MidpointRounding.AwayFromZero))
                    : Math.Max(0m, lineAmount);

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

        /// <summary>
        /// Lee AVS_INTEGRAFAST_02 y actualiza el request con COD_SUCURSAL, CEDULA_TRIBUTARIA,
        /// COMPROBANTE_INTERNO, TIPOCAMBIO y TERMINAL_POS corregidos.
        /// Debe llamarse ANTES de EnsureClaves para que CLAVE50/CLAVE20 usen los valores correctos.
        /// </summary>
        private static void ApplyIntegraFast02Config(SqlConnection cn, NovaRetailCreateSaleRequest request)
        {
            var comprobanteTipo = request.COMPROBANTE_TIPO ?? string.Empty;
            var consecutivoCol = GetConsecutivoColumnIntegraFast02(comprobanteTipo);

            if (consecutivoCol != null)
            {
                try
                {
                    using (var incCmd = new SqlCommand(
                        $"UPDATE dbo.AVS_INTEGRAFAST_02 SET {consecutivoCol} = {consecutivoCol} + 1 " +
                        $"OUTPUT INSERTED.{consecutivoCol} AS Consecutivo, INSERTED.COD_SUCURSAL, INSERTED.PROVEEDOR_SISTEMA", cn))
                    {
                        incCmd.CommandTimeout = 30;
                        using (var rd = incCmd.ExecuteReader())
                        {
                            if (rd.Read())
                            {
                                request.COMPROBANTE_INTERNO = Convert.ToInt32(rd["Consecutivo"]).ToString();
                                var suc = rd["COD_SUCURSAL"];
                                if (suc != DBNull.Value && !string.IsNullOrWhiteSpace(Convert.ToString(suc)))
                                    request.COD_SUCURSAL = Convert.ToString(suc);
                                var ced = rd["PROVEEDOR_SISTEMA"];
                                if (ced != DBNull.Value && !string.IsNullOrWhiteSpace(Convert.ToString(ced)))
                                    request.CedulaTributaria = Convert.ToString(ced);
                            }
                        }
                    }
                }
                catch { /* tabla AVS_INTEGRAFAST_02 no existe — usar valores del request */ }
            }

            // Corregir TIPOCAMBIO: si la moneda es CRC el tipo de cambio es 1
            if (string.Equals(request.CurrencyCode, "CRC", StringComparison.OrdinalIgnoreCase))
                request.TipoCambio = "1";

            // Corregir TERMINAL_POS: eliminar ceros a la izquierda
            if (!string.IsNullOrWhiteSpace(request.TERMINAL_POS) && int.TryParse(request.TERMINAL_POS, out int terminalPosNum))
                request.TERMINAL_POS = terminalPosNum.ToString();
        }

        private static void EnsureTiqueteEspera(SqlConnection cn, NovaRetailCreateSaleRequest request, int transactionNumber)
        {
            using (var existsCmd = new SqlCommand("SELECT COUNT(1) FROM dbo.AVS_INTEGRAFAST_01 WHERE TRANSACTIONNUMBER = @TransactionNumber", cn))
            {
                existsCmd.Parameters.AddWithValue("@TransactionNumber", transactionNumber.ToString());
                if (Convert.ToInt32(existsCmd.ExecuteScalar()) > 0)
                    return;
            }

            var medioPagos = (request.Tenders ?? new List<NovaRetailSaleTenderDto>())
                .OrderBy(t => t.RowNo)
                .Select(t => string.IsNullOrWhiteSpace(t.MedioPagoCodigo) ? string.Empty : t.MedioPagoCodigo.Trim())
                .Take(4)
                .ToList();

            while (medioPagos.Count < 4)
                medioPagos.Add(string.Empty);

            // Valores ya corregidos por ApplyIntegraFast02Config
            string codSucursal = request.COD_SUCURSAL ?? string.Empty;
            string cedulaTributaria = request.CedulaTributaria ?? string.Empty;
            string comprobanteInterno = string.IsNullOrWhiteSpace(request.COMPROBANTE_INTERNO)
                ? transactionNumber.ToString()
                : request.COMPROBANTE_INTERNO;
            string tipoCambio = request.TipoCambio ?? "1";
            string terminalPos = request.TERMINAL_POS ?? string.Empty;
            string comprobanteTipo = request.COMPROBANTE_TIPO ?? string.Empty;

            // Intentar primero via SP, si falla usar INSERT directo
            try
            {
                using (var cmd = new SqlCommand("dbo.spAVS_InsertTiqueteEspera", cn))
                {
                    cmd.CommandType = CommandType.StoredProcedure;
                    cmd.CommandTimeout = 120;
                    cmd.Parameters.AddWithValue("@CLAVE50", request.CLAVE50 ?? string.Empty);
                    cmd.Parameters.AddWithValue("@CLAVE20", request.CLAVE20 ?? string.Empty);
                    cmd.Parameters.AddWithValue("@TRANSACTIONNUMBER", transactionNumber.ToString());
                    cmd.Parameters.AddWithValue("@COD_SUCURSAL", codSucursal);
                    cmd.Parameters.AddWithValue("@TERMINAL_POS", terminalPos);
                    cmd.Parameters.AddWithValue("@COMPROBANTE_INTERNO", comprobanteInterno);
                    cmd.Parameters.AddWithValue("@COMPROBANTE_SITUACION", request.COMPROBANTE_SITUACION ?? string.Empty);
                    cmd.Parameters.AddWithValue("@COMPROBANTE_TIPO", comprobanteTipo);
                    cmd.Parameters.AddWithValue("@CURRENCYCODE", request.CurrencyCode ?? "CRC");
                    cmd.Parameters.AddWithValue("@CONDICIONVENTA", request.CondicionVenta ?? "01");
                    cmd.Parameters.AddWithValue("@COD_CLIENTE", request.CodCliente ?? string.Empty);
                    cmd.Parameters.AddWithValue("@NOMBRE_CLIENTE", request.NombreCliente ?? string.Empty);
                    cmd.Parameters.AddWithValue("@MEDIO_PAGO1", medioPagos[0]);
                    cmd.Parameters.AddWithValue("@MEDIO_PAGO2", medioPagos[1]);
                    cmd.Parameters.AddWithValue("@MEDIO_PAGO3", medioPagos[2]);
                    cmd.Parameters.AddWithValue("@MEDIO_PAGO4", medioPagos[3]);
                    cmd.Parameters.AddWithValue("@TIPOCAMBIO", tipoCambio);
                    cmd.Parameters.AddWithValue("@CEDULA_TRIBUTARIA", cedulaTributaria);
                    cmd.Parameters.AddWithValue("@EXONERA", request.Exonera);
                    cmd.Parameters.AddWithValue("@NC_TIPO_DOC", request.NC_TIPO_DOC ?? string.Empty);
                    cmd.Parameters.AddWithValue("@NC_REFERENCIA", request.NC_REFERENCIA ?? string.Empty);
                    cmd.Parameters.AddWithValue("@NC_REFERENCIA_FECHA", (object)request.NC_REFERENCIA_FECHA ?? DBNull.Value);
                    cmd.Parameters.AddWithValue("@NC_CODIGO", request.NC_CODIGO ?? string.Empty);
                    cmd.Parameters.AddWithValue("@NC_RAZON", request.NC_RAZON ?? string.Empty);
                    cmd.Parameters.AddWithValue("@TR_REP", request.TR_REP ?? string.Empty);
                    cmd.ExecuteNonQuery();
                }
            }
            catch
            {
                // SP no existe — INSERT directo
                InsertIntegraFast01Direct(cn, request, transactionNumber, medioPagos,
                    codSucursal, terminalPos, comprobanteInterno, tipoCambio, cedulaTributaria);
            }
        }

        private static void InsertIntegraFast01Direct(SqlConnection cn, NovaRetailCreateSaleRequest request, int transactionNumber, List<string> medioPagos,
            string codSucursal, string terminalPos, string comprobanteInterno, string tipoCambio, string cedulaTributaria)
        {
            using (var cmd = new SqlCommand(@"
                INSERT INTO dbo.AVS_INTEGRAFAST_01
                    (CLAVE50, CLAVE20, TRANSACTIONNUMBER, COD_SUCURSAL, TERMINAL_POS,
                     COMPROBANTE_INTERNO, COMPROBANTE_SITUACION, COMPROBANTE_TIPO,
                     COD_MONEDA, CONDICION_VENTA, COD_CLIENTE, NOMBRE_CLIENTE,
                     MEDIO_PAGO1, MEDIO_PAGO2, MEDIO_PAGO3, MEDIO_PAGO4,
                     TIPOCAMBIO, CEDULA_TRIBUTARIA, EXONERA,
                     NC_TIPO_DOC, NC_REFERENCIA, NC_REFERENCIA_FECHA, NC_CODIGO, NC_RAZON,
                     FECHA_TRANSAC, ESTADO_HACIENDA)
                VALUES
                    (@CLAVE50, @CLAVE20, @TN, @COD_SUCURSAL, @TERMINAL_POS,
                     @COMPROBANTE_INTERNO, @COMPROBANTE_SITUACION, @COMPROBANTE_TIPO,
                     @CURRENCYCODE, @CONDICIONVENTA, @COD_CLIENTE, @NOMBRE_CLIENTE,
                     @MEDIO_PAGO1, @MEDIO_PAGO2, @MEDIO_PAGO3, @MEDIO_PAGO4,
                     @TIPOCAMBIO, @CEDULA_TRIBUTARIA, @EXONERA,
                     @NC_TIPO_DOC, @NC_REFERENCIA, @NC_REFERENCIA_FECHA, @NC_CODIGO, @NC_RAZON,
                     GETDATE(), '00')", cn))
            {
                cmd.CommandTimeout = 60;
                cmd.Parameters.AddWithValue("@CLAVE50", request.CLAVE50 ?? string.Empty);
                cmd.Parameters.AddWithValue("@CLAVE20", request.CLAVE20 ?? string.Empty);
                cmd.Parameters.AddWithValue("@TN", transactionNumber.ToString());
                cmd.Parameters.AddWithValue("@COD_SUCURSAL", codSucursal);
                cmd.Parameters.AddWithValue("@TERMINAL_POS", terminalPos);
                cmd.Parameters.AddWithValue("@COMPROBANTE_INTERNO", comprobanteInterno);
                cmd.Parameters.AddWithValue("@COMPROBANTE_SITUACION", request.COMPROBANTE_SITUACION ?? string.Empty);
                cmd.Parameters.AddWithValue("@COMPROBANTE_TIPO", request.COMPROBANTE_TIPO ?? string.Empty);
                cmd.Parameters.AddWithValue("@CURRENCYCODE", request.CurrencyCode ?? "CRC");
                cmd.Parameters.AddWithValue("@CONDICIONVENTA", request.CondicionVenta ?? "01");
                cmd.Parameters.AddWithValue("@COD_CLIENTE", request.CodCliente ?? string.Empty);
                cmd.Parameters.AddWithValue("@NOMBRE_CLIENTE", request.NombreCliente ?? string.Empty);
                cmd.Parameters.AddWithValue("@MEDIO_PAGO1", medioPagos[0]);
                cmd.Parameters.AddWithValue("@MEDIO_PAGO2", medioPagos[1]);
                cmd.Parameters.AddWithValue("@MEDIO_PAGO3", medioPagos[2]);
                cmd.Parameters.AddWithValue("@MEDIO_PAGO4", medioPagos[3]);
                cmd.Parameters.AddWithValue("@TIPOCAMBIO", tipoCambio);
                cmd.Parameters.AddWithValue("@CEDULA_TRIBUTARIA", cedulaTributaria);
                cmd.Parameters.AddWithValue("@EXONERA", request.Exonera);
                cmd.Parameters.AddWithValue("@NC_TIPO_DOC", request.NC_TIPO_DOC ?? string.Empty);
                cmd.Parameters.AddWithValue("@NC_REFERENCIA", request.NC_REFERENCIA ?? string.Empty);
                cmd.Parameters.AddWithValue("@NC_REFERENCIA_FECHA", (object)request.NC_REFERENCIA_FECHA ?? DBNull.Value);
                cmd.Parameters.AddWithValue("@NC_CODIGO", request.NC_CODIGO ?? string.Empty);
                cmd.Parameters.AddWithValue("@NC_RAZON", request.NC_RAZON ?? string.Empty);
                cmd.ExecuteNonQuery();
            }
        }

        private static readonly HashSet<string> AllowedConsecutivoColumns =
            new HashSet<string>(StringComparer.OrdinalIgnoreCase) { "CN_FE", "CN_ND", "CN_NC", "CN_TE", "CN_FEX" };

        /// <summary>
        /// Mapea COMPROBANTE_TIPO al nombre de la columna de consecutivo en AVS_INTEGRAFAST_02.
        /// </summary>
        private static string GetConsecutivoColumnIntegraFast02(string comprobanteTipo)
        {
            string col;
            switch (comprobanteTipo)
            {
                case "01": col = "CN_FE";  break;
                case "02": col = "CN_ND";  break;
                case "03": col = "CN_NC";  break;
                case "04": col = "CN_TE";  break;
                case "09": col = "CN_FEX"; break;
                default:   return null;
            }

            if (!AllowedConsecutivoColumns.Contains(col))
                return null;

            return col;
        }

        /// <summary>
        /// Inserta las líneas de detalle en AVS_INTEGRAFAST_05 para que fxAVS_GetLineaDetalle
        /// pueda leer los datos fiscales (Cabys, IVA, etc.) al procesar con IntegraFast.
        /// </summary>
        private static void EnsureIntegraFast05(SqlConnection cn, NovaRetailCreateSaleRequest request, int transactionNumber)
        {
            if (request.Items == null || request.Items.Count == 0)
                return;

            // Leer CLAVE50 ya generada en AVS_INTEGRAFAST_01
            string clave50;
            using (var cmd = new SqlCommand("SELECT TOP 1 CLAVE50 FROM dbo.AVS_INTEGRAFAST_01 WHERE TRANSACTIONNUMBER = @TN", cn))
            {
                cmd.Parameters.AddWithValue("@TN", transactionNumber.ToString());
                var val = cmd.ExecuteScalar();
                clave50 = val != null && val != DBNull.Value ? Convert.ToString(val) : string.Empty;
            }

            if (string.IsNullOrWhiteSpace(clave50))
                return;

            // Verificar si ya existen registros
            using (var chk = new SqlCommand("SELECT COUNT(1) FROM dbo.AVS_INTEGRAFAST_05 WHERE CLAVE50 = @CLAVE50", cn))
            {
                chk.Parameters.AddWithValue("@CLAVE50", clave50);
                try
                {
                    if (Convert.ToInt32(chk.ExecuteScalar()) > 0)
                        return;
                }
                catch
                {
                    CreateIntegraFast05Table(cn);
                }
            }

            var taxSystem = GetTaxSystem(cn);
            int numLinea = 0;

            // Cargar info de artículos (Cabys, Code) desde Item table
            var itemInfoMap = new Dictionary<int, (string Cabys, string Code, string Description)>();
            var itemIds = request.Items.Select(i => i.ItemID).Distinct().ToList();
            if (itemIds.Count > 0)
            {
                var parameters = itemIds.Select((id, i) => $"@id{i}").ToList();
                var idList = string.Join(",", parameters);
                try
                {
                    using (var cmd = new SqlCommand(
                        $"SELECT ID, ISNULL(CAST(ExtendedDescription AS NVARCHAR(20)),'') AS Cabys, ISNULL(ItemLookupCode,'') AS Code, ISNULL(Description,'') AS Desc1 FROM dbo.Item WHERE ID IN ({idList})", cn))
                    {
                        for (int pi = 0; pi < itemIds.Count; pi++)
                            cmd.Parameters.AddWithValue($"@id{pi}", itemIds[pi]);

                        using (var r = cmd.ExecuteReader())
                        {
                            while (r.Read())
                            {
                                var id = Convert.ToInt32(r["ID"]);
                                var cabys = r["Cabys"]?.ToString() ?? string.Empty;
                                var code = r["Code"]?.ToString() ?? string.Empty;
                                var desc = r["Desc1"]?.ToString() ?? string.Empty;
                                itemInfoMap[id] = (cabys, code, desc);
                            }
                        }
                    }
                }
                catch { /* tabla Item podría tener diferente esquema */ }
            }

            foreach (var item in request.Items.OrderBy(i => i.RowNo))
            {
                numLinea++;
                var qty = item.Quantity <= 0 ? 1m : item.Quantity;
                var unitPrice = item.UnitPrice;
                var fullPrice = item.FullPrice ?? unitPrice;
                var montoTotal = Math.Round(fullPrice * qty, 2);
                var montoDescuento = Math.Round(item.LineDiscountAmount, 2);
                if (montoDescuento == 0m && fullPrice > unitPrice)
                    montoDescuento = Math.Round((fullPrice - unitPrice) * qty, 2);
                var subTotal = montoTotal - montoDescuento;
                var taxRate = item.SalesTax > 0 && subTotal > 0
                    ? Math.Round(item.SalesTax / subTotal * 100m, 2)
                    : 0m;
                var montoImpuesto = Math.Round(item.SalesTax, 2);
                var montoLinea = subTotal + montoImpuesto;
                var hasExoneration = !string.IsNullOrWhiteSpace(item.ExNumeroDoc);

                itemInfoMap.TryGetValue(item.ItemID, out var info);
                var cabys = info.Cabys ?? string.Empty;
                var codProducto = info.Code ?? item.ItemID.ToString();
                var detalle = !string.IsNullOrWhiteSpace(item.ExtendedDescription)
                    ? item.ExtendedDescription
                    : !string.IsNullOrWhiteSpace(info.Description) ? info.Description
                    : item.ItemID.ToString();

                // Código de tarifa IVA según normativa CR
                var codTarifaIVA = taxRate >= 13m ? "08" : taxRate >= 4m ? "04" : taxRate >= 2m ? "07" : taxRate >= 1m ? "06" : "01";
                var codImpuesto = taxRate > 0 ? "01" : string.Empty;

                var naturalezaDescuento = montoDescuento > 0 ? (item.LineComment ?? "Descuento comercial") : string.Empty;

                // Calcular porcentaje exoneración para EXONERA_PORCENTAJE_COMPRA
                var exoneraPorcentaje = item.ExPorcentaje;

                try
                {
                    using (var cmd = new SqlCommand(@"
                        INSERT INTO dbo.AVS_INTEGRAFAST_05
                            (CLAVE50, TRANSACTIONNUMBER, NUM_LINEA, ID_PRODUCTO, CANTIDAD, UNIDAD_MEDIDA,
                             DETALLE, PRECIO_UNITARIO, MONTO_TOTAL, MONTO_DESCUENTO, NATURALEZA_DESCUENTO,
                             SUBTOTAL, COD_IMPUESTO, COD_IMPUESTO_BASE, TARIFA_IMPUESTO, MONTO_IMPUESTO,
                             EXONERA_TIPO_DOCUMENTO, EXONERA_NUMERO_DOCUMENTO, EXONERA_INSTITUCION,
                             EXONERA_FECHA_EMISION, EXONERA_MONTO_IMPUESTO, EXONERA_PORCENTAJE_COMPRA,
                             EXONERA_TOTAL_LINEA, SyncGuid, ARTICULO, INCISO)
                        VALUES
                            (@CLAVE50, @TN, @NUM_LINEA, @ID_PRODUCTO, @CANTIDAD, @UNIDAD_MEDIDA,
                             @DETALLE, @PRECIOUNIT, @MONTO_TOTAL, @MONTO_DESCUENTO, @NATURALEZA_DESCUENTO,
                             @SUBTOTAL, @COD_IMPUESTO, @COD_IMPUESTO_BASE, @TARIFA_IMPUESTO, @MONTO_IMPUESTO,
                             @EXONERA_TIPO_DOCUMENTO, @EXONERA_NUMERO_DOCUMENTO, @EXONERA_INSTITUCION,
                             @EXONERA_FECHA_EMISION, @EXONERA_MONTO_IMPUESTO, @EXONERA_PORCENTAJE_COMPRA,
                             @EXONERA_TOTAL_LINEA, NEWID(), @ARTICULO, @INCISO)", cn))
                    {
                        cmd.Parameters.AddWithValue("@CLAVE50", clave50);
                        cmd.Parameters.AddWithValue("@TN", transactionNumber.ToString());
                        cmd.Parameters.AddWithValue("@NUM_LINEA", numLinea);
                        cmd.Parameters.AddWithValue("@ID_PRODUCTO", Truncate(item.ItemID.ToString(), 15));
                        cmd.Parameters.AddWithValue("@CANTIDAD", qty);
                        cmd.Parameters.AddWithValue("@UNIDAD_MEDIDA", "Und");
                        cmd.Parameters.AddWithValue("@DETALLE", Truncate(detalle, 160));
                        cmd.Parameters.AddWithValue("@PRECIOUNIT", Math.Round(fullPrice, 5));
                        cmd.Parameters.AddWithValue("@MONTO_TOTAL", montoTotal);
                        cmd.Parameters.AddWithValue("@MONTO_DESCUENTO", montoDescuento);
                        cmd.Parameters.AddWithValue("@NATURALEZA_DESCUENTO", Truncate(naturalezaDescuento, 80));
                        cmd.Parameters.AddWithValue("@SUBTOTAL", subTotal);
                        cmd.Parameters.AddWithValue("@COD_IMPUESTO", codImpuesto);
                        cmd.Parameters.AddWithValue("@COD_IMPUESTO_BASE", string.Empty);
                        cmd.Parameters.AddWithValue("@TARIFA_IMPUESTO", taxRate > 0 ? taxRate : 0m);
                        cmd.Parameters.AddWithValue("@MONTO_IMPUESTO", montoImpuesto);
                        cmd.Parameters.AddWithValue("@EXONERA_TIPO_DOCUMENTO", hasExoneration ? (object)Truncate(item.ExTipoDoc, 2) : DBNull.Value);
                        cmd.Parameters.AddWithValue("@EXONERA_NUMERO_DOCUMENTO", hasExoneration ? (object)Truncate(item.ExNumeroDoc, 40) : DBNull.Value);
                        cmd.Parameters.AddWithValue("@EXONERA_INSTITUCION", hasExoneration ? (object)Truncate(item.ExInstitucion, 100) : DBNull.Value);
                        cmd.Parameters.AddWithValue("@EXONERA_FECHA_EMISION", hasExoneration && item.ExFecha.HasValue ? (object)item.ExFecha.Value.ToString("yyyy-MM-dd") : DBNull.Value);
                        cmd.Parameters.AddWithValue("@EXONERA_MONTO_IMPUESTO", hasExoneration ? (object)item.ExMonto : DBNull.Value);
                        cmd.Parameters.AddWithValue("@EXONERA_PORCENTAJE_COMPRA", hasExoneration ? (object)Convert.ToInt16(Math.Round(exoneraPorcentaje, 0, MidpointRounding.AwayFromZero)) : DBNull.Value);
                        cmd.Parameters.AddWithValue("@EXONERA_TOTAL_LINEA", hasExoneration ? (object)montoLinea : DBNull.Value);
                        cmd.Parameters.AddWithValue("@ARTICULO", Truncate(codProducto, 6));
                        cmd.Parameters.AddWithValue("@INCISO", Truncate(cabys, 6));
                        cmd.ExecuteNonQuery();
                    }
                }
                catch (Exception ex)
                {
                    throw new InvalidOperationException($"No se pudo insertar AVS_INTEGRAFAST_05 para ItemID {item.ItemID}.", ex);
                }
            }
        }

        /// <summary>Crea la tabla AVS_INTEGRAFAST_05 si no existe.</summary>
        private static void CreateIntegraFast05Table(SqlConnection cn)
        {
            try
            {
                using (var cmd = new SqlCommand(@"
                    IF NOT EXISTS (SELECT 1 FROM INFORMATION_SCHEMA.TABLES WHERE TABLE_NAME = 'AVS_INTEGRAFAST_05')
                    CREATE TABLE dbo.AVS_INTEGRAFAST_05 (
                        ID                        INT IDENTITY(1,1) PRIMARY KEY,
                        CLAVE50                   NVARCHAR(50)  NOT NULL DEFAULT '',
                        TRANSACTIONNUMBER         NVARCHAR(20)  NOT NULL DEFAULT '',
                        NUM_LINEA                 INT           NOT NULL DEFAULT 0,
                        ID_PRODUCTO               NVARCHAR(15)  NOT NULL DEFAULT '',
                        CANTIDAD                  DECIMAL(18,4) NOT NULL DEFAULT 0,
                        UNIDAD_MEDIDA             NVARCHAR(15)  NOT NULL DEFAULT 'Und',
                        DETALLE                   NVARCHAR(160) NOT NULL DEFAULT '',
                        PRECIO_UNITARIO           DECIMAL(18,5) NOT NULL DEFAULT 0,
                        MONTO_TOTAL               DECIMAL(18,2) NOT NULL DEFAULT 0,
                        MONTO_DESCUENTO           DECIMAL(18,2) NOT NULL DEFAULT 0,
                        NATURALEZA_DESCUENTO      NVARCHAR(80)  NOT NULL DEFAULT '',
                        SUBTOTAL                  DECIMAL(18,2) NOT NULL DEFAULT 0,
                        COD_IMPUESTO              NVARCHAR(2)   NOT NULL DEFAULT '',
                        COD_IMPUESTO_BASE         NVARCHAR(2)   NULL,
                        TARIFA_IMPUESTO           DECIMAL(18,5) NOT NULL DEFAULT 0,
                        MONTO_IMPUESTO            DECIMAL(18,2) NOT NULL DEFAULT 0,
                        EXONERA_TIPO_DOCUMENTO    NVARCHAR(2)   NULL,
                        EXONERA_NUMERO_DOCUMENTO  NVARCHAR(40)  NULL,
                        EXONERA_INSTITUCION       NVARCHAR(100) NULL,
                        EXONERA_FECHA_EMISION     NVARCHAR(25)  NULL,
                        EXONERA_MONTO_IMPUESTO    DECIMAL(18,2) NULL,
                        EXONERA_PORCENTAJE_COMPRA SMALLINT      NULL,
                        EXONERA_TOTAL_LINEA       DECIMAL(18,2) NULL,
                        SyncGuid                  UNIQUEIDENTIFIER NOT NULL DEFAULT NEWID(),
                        ARTICULO                  NVARCHAR(6)   NULL,
                        INCISO                    NVARCHAR(6)   NULL
                    )", cn))
                {
                    cmd.ExecuteNonQuery();
                }
            }
            catch { /* entorno sin permisos DDL */ }
        }

        private static int GetTaxSystem(SqlConnection cn)
        {
            using (var cmd = new SqlCommand("SELECT TOP 1 TaxSystem FROM dbo.[Configuration]", cn))
            {
                var value = cmd.ExecuteScalar();
                return value == null || value == DBNull.Value ? 0 : Convert.ToInt32(value);
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

        private static void EnsureExonerationEntries(SqlConnection cn, NovaRetailCreateSaleRequest request, int transactionNumber)
        {
            var exonerationItems = (request.Items ?? new List<NovaRetailSaleItemDto>())
                .Where(i => !string.IsNullOrWhiteSpace(i.ExNumeroDoc))
                .ToList();

            if (exonerationItems.Count == 0)
                return;

            string clave50;
            using (var cmd = new SqlCommand("SELECT TOP 1 CLAVE50 FROM dbo.AVS_INTEGRAFAST_01 WHERE TRANSACTIONNUMBER = @TN", cn))
            {
                cmd.Parameters.AddWithValue("@TN", transactionNumber.ToString());
                var val = cmd.ExecuteScalar();
                clave50 = val != null && val != DBNull.Value ? Convert.ToString(val) : string.Empty;
            }

            if (string.IsNullOrWhiteSpace(clave50))
                return;

            foreach (var item in exonerationItems)
            {
                using (var cmd = new SqlCommand(@"
                    INSERT INTO dbo.AVS_INTEGRAFAST_01_EXONERA
                        (CLAVE50, ITEMID, EX_TARIFA_PORC, EX_TARIFA_MONTO, EX_TIPODOC, EX_NUMERODOC, EX_INSTITUCION, EX_FECHA, EX_MONTO, EX_PORCENTAJE, SyncGuid)
                    VALUES
                        (@CLAVE50, @ITEMID, @EX_TARIFA_PORC, @EX_TARIFA_MONTO, @EX_TIPODOC, @EX_NUMERODOC, @EX_INSTITUCION, @EX_FECHA, @EX_MONTO, @EX_PORCENTAJE, NEWID())", cn))
                {
                    cmd.Parameters.AddWithValue("@CLAVE50", clave50);
                    cmd.Parameters.AddWithValue("@ITEMID", item.ItemID);
                    cmd.Parameters.AddWithValue("@EX_TARIFA_PORC", item.ExPorcentaje);
                    cmd.Parameters.AddWithValue("@EX_TARIFA_MONTO", item.ExMonto);
                    cmd.Parameters.AddWithValue("@EX_TIPODOC", Truncate(item.ExTipoDoc, 2));
                    cmd.Parameters.AddWithValue("@EX_NUMERODOC", Truncate(item.ExNumeroDoc, 17));
                    cmd.Parameters.AddWithValue("@EX_INSTITUCION", Truncate(item.ExInstitucion, 100));
                    cmd.Parameters.AddWithValue("@EX_FECHA", (object)(item.ExFecha ?? DateTime.Today));
                    cmd.Parameters.AddWithValue("@EX_MONTO", item.ExMonto);
                    cmd.Parameters.AddWithValue("@EX_PORCENTAJE", item.ExPorcentaje);
                    cmd.ExecuteNonQuery();
                }
            }
        }
            private static string Truncate(string value, int maxLength)
            {
                var s = value ?? string.Empty;
                return s.Length <= maxLength ? s : s.Substring(0, maxLength);
            }

        /// <summary>
        /// Creates an AR_Transaction entry when the sale is a credit sale or a credit-mode credit note.
        /// Credit sale (CondicionVenta=02, no NC): DocumentType=2 (Invoice) – increases customer debt.
        /// Credit NC (has NC_REFERENCIA, MedioPago 99): DocumentType=3 (Credit Memo) – reduces customer debt.
        /// </summary>
        private static void TryCreateARTransaction(NovaRetailCreateSaleRequest request, NovaRetailCreateSaleResponse response)
        {
            if (response.TransactionNumber <= 0 || string.IsNullOrWhiteSpace(request.CodCliente))
                return;

            var total = Math.Abs(response.Total ?? 0m);
            if (total <= 0m)
                return;

            bool isNC = !string.IsNullOrWhiteSpace(request.NC_REFERENCIA);
            bool isCreditSale = string.Equals(request.CondicionVenta, "02", StringComparison.OrdinalIgnoreCase) && !isNC;
            bool isCreditNC = isNC && request.Tenders != null &&
                              request.Tenders.Any(t => string.Equals(t.MedioPagoCodigo, "99", StringComparison.OrdinalIgnoreCase));

            if (!isCreditSale && !isCreditNC)
                return;

            var connectionString = GetConnectionString();

            using (var cn = new SqlConnection(connectionString))
            {
                cn.Open();

                var customerAccountNumber = request.CodCliente;
                if (request.CustomerID > 0)
                {
                    using (var cmd = new SqlCommand("SELECT TOP 1 AccountNumber FROM dbo.Customer WHERE ID = @CustomerID", cn))
                    {
                        cmd.Parameters.AddWithValue("@CustomerID", request.CustomerID);
                        var result = cmd.ExecuteScalar();
                        if (result != null && result != DBNull.Value && !string.IsNullOrWhiteSpace(Convert.ToString(result)))
                            customerAccountNumber = Convert.ToString(result);
                    }
                }

                if (string.IsNullOrWhiteSpace(customerAccountNumber))
                    return;

                // Resolve AR_Account.ID for the customer
                int accountID = 0;
                using (var cmd = new SqlCommand("SELECT TOP 1 ID FROM dbo.AR_Account WHERE Number = @Number", cn))
                {
                    cmd.Parameters.AddWithValue("@Number", customerAccountNumber);
                    var result = cmd.ExecuteScalar();
                    if (result != null && result != DBNull.Value)
                        accountID = Convert.ToInt32(result);
                }

                if (accountID <= 0)
                    return;

                // Resolve Customer.ID
                int customerID = request.CustomerID;
                if (customerID <= 0)
                {
                    using (var cmd = new SqlCommand("SELECT TOP 1 ID FROM dbo.Customer WHERE AccountNumber = @Acct", cn))
                    {
                        cmd.Parameters.AddWithValue("@Acct", customerAccountNumber);
                        var result = cmd.ExecuteScalar();
                        if (result != null && result != DBNull.Value)
                            customerID = Convert.ToInt32(result);
                    }
                }

                var now = DateTime.Now;
                var reference = "TR:" + response.TransactionNumber;

                // Credit sale: DocumentType=3 (Invoice), LedgerType=3, Positive=1, amount positive → increases balance
                // Credit NC:  DocumentType=4 (Credit Memo), LedgerType=3, Positive=1, amount negative → decreases balance
                byte documentType = (byte)(isCreditSale ? 3 : 4);
                byte ledgerType = 3;
                decimal amountACY = isCreditSale ? total : -total;

                // 1. Insert AR_LedgerEntry
                int ledgerEntryID = 0;
                using (var cmd = new SqlCommand("dbo.OFF_AR_LEDGERENTRY_INSERT", cn))
                {
                    cmd.CommandType = CommandType.StoredProcedure;
                    cmd.CommandTimeout = 60;

                    cmd.Parameters.AddWithValue("@LastUpdated", now);
                    cmd.Parameters.AddWithValue("@AccountID", accountID);
                    cmd.Parameters.AddWithValue("@CustomerID", customerID);
                    cmd.Parameters.AddWithValue("@StoreID", request.StoreID);
                    cmd.Parameters.AddWithValue("@LinkType", (byte)0);
                    cmd.Parameters.AddWithValue("@LinkID", 0);
                    cmd.Parameters.AddWithValue("@AuditEntryID", 0);
                    cmd.Parameters.AddWithValue("@DocumentType", documentType);
                    cmd.Parameters.AddWithValue("@DocumentID", 0);
                    cmd.Parameters.AddWithValue("@PostingDate", now);
                    cmd.Parameters.AddWithValue("@DueDate", now.AddDays(30));
                    cmd.Parameters.AddWithValue("@LedgerType", ledgerType);
                    cmd.Parameters.AddWithValue("@Reference", reference);
                    cmd.Parameters.AddWithValue("@Description", isCreditSale ? "Venta a crédito" : "Nota de crédito");
                    cmd.Parameters.AddWithValue("@CurrencyID", 0);
                    cmd.Parameters.AddWithValue("@CurrencyFactor", 1.0);
                    cmd.Parameters.AddWithValue("@Positive", true);
                    cmd.Parameters.AddWithValue("@ClosingDate", DBNull.Value);
                    cmd.Parameters.AddWithValue("@ReasonID", 0);
                    cmd.Parameters.AddWithValue("@HoldReasonID", 0);
                    cmd.Parameters.AddWithValue("@UndoReasonID", 0);
                    cmd.Parameters.AddWithValue("@PayMethodID", 0);
                    cmd.Parameters.AddWithValue("@TransactionID", 0);
                    cmd.Parameters.AddWithValue("@ExtReference", string.Empty);
                    cmd.Parameters.AddWithValue("@Comment", string.Empty);

                    using (var reader = cmd.ExecuteReader())
                    {
                        if (reader.Read())
                            ledgerEntryID = Convert.ToInt32(reader["ID"]);
                    }
                }

                if (ledgerEntryID <= 0)
                    return;

                // 2. Insert AR_LedgerEntryDetail
                using (var cmd = new SqlCommand("dbo.OFF_AR_LEDGERENTRYDETAIL_INSERT", cn))
                {
                    cmd.CommandType = CommandType.StoredProcedure;
                    cmd.CommandTimeout = 60;

                    cmd.Parameters.AddWithValue("@LedgerEntryID", ledgerEntryID);
                    cmd.Parameters.AddWithValue("@AccountID", accountID);
                    cmd.Parameters.AddWithValue("@LedgerType", ledgerType);
                    cmd.Parameters.AddWithValue("@DueDate", now.AddDays(30));
                    cmd.Parameters.AddWithValue("@PostingDate", now);
                    cmd.Parameters.AddWithValue("@DetailType", (byte)0);
                    cmd.Parameters.AddWithValue("@Reference", reference);
                    cmd.Parameters.AddWithValue("@Amount", amountACY);
                    cmd.Parameters.AddWithValue("@AmountLCY", amountACY);
                    cmd.Parameters.AddWithValue("@AmountACY", amountACY);
                    cmd.Parameters.AddWithValue("@AuditEntryID", 0);
                    cmd.Parameters.AddWithValue("@AppliedEntryID", 0);
                    cmd.Parameters.AddWithValue("@AppliedAmount", 0m);
                    cmd.Parameters.AddWithValue("@UnapplyEntryID", 0);
                    cmd.Parameters.AddWithValue("@UnapplyReasonID", 0);
                    cmd.Parameters.AddWithValue("@ISCLOSING", false);

                    cmd.ExecuteNonQuery();
                }
            }
        }

        [HttpPost]
        [Route("create-quote")]
        public HttpResponseMessage CreateQuote([FromBody] NovaRetailCreateQuoteRequest request)
        {
            if (request == null)
            {
                return Request.CreateResponse(HttpStatusCode.BadRequest, new NovaRetailCreateQuoteResponse
                {
                    Ok = false,
                    Message = "Solicitud inválida."
                });
            }

            if (!ModelState.IsValid)
            {
                var errors = ModelState
                    .Where(x => x.Value != null && x.Value.Errors.Count > 0)
                    .SelectMany(x => x.Value.Errors.Select(e => string.IsNullOrWhiteSpace(x.Key)
                        ? e.ErrorMessage
                        : $"{x.Key}: {e.ErrorMessage}"))
                    .ToList();

                return Request.CreateResponse(HttpStatusCode.BadRequest, new NovaRetailCreateQuoteResponse
                {
                    Ok = false,
                    Message = errors.Count == 0
                        ? "Solicitud inválida."
                        : string.Join(" | ", errors)
                });
            }

            if (request.Items == null || request.Items.Count == 0)
            {
                return Request.CreateResponse(HttpStatusCode.BadRequest, new NovaRetailCreateQuoteResponse
                {
                    Ok = false,
                    Message = "La cotización no contiene ítems."
                });
            }

            var response = new NovaRetailCreateQuoteResponse();
            var connectionString = GetConnectionString();

            try
            {
                using (var cn = new SqlConnection(connectionString))
                {
                    cn.Open();

                    using (var tx = cn.BeginTransaction())
                    {
                        try
                        {
                            var now = DateTime.Now;
                            var expiration = request.ExpirationOrDueDate ?? now.AddDays(30);
                            var syncGuid = Guid.NewGuid();

                            // INSERT [Order] con Type=3 (Cotización)
                            int orderID;
                            using (var cmd = new SqlCommand(@"
                                INSERT INTO [Order]
                                    ([StoreID],[Closed],[Time],[Type],[Comment],[CustomerID],[ShipToID],
                                     [DepositOverride],[Deposit],[Tax],[Total],[LastUpdated],
                                     [ExpirationOrDueDate],[Taxable],[SalesRepID],[ReferenceNumber],
                                     [ShippingChargeOnOrder],[ShippingChargeOverride],[ShippingServiceID],
                                     [ShippingTrackingNumber],[ShippingNotes],[ReasonCodeID],[ExchangeID],
                                     [ChannelType],[DefaultDiscountReasonCodeID],[DefaultReturnReasonCodeID],
                                     [DefaultTaxChangeReasonCodeID],[SyncGuid])
                                VALUES
                                    (@StoreID,@Closed,@Time,@Type,@Comment,@CustomerID,@ShipToID,
                                     @DepositOverride,@Deposit,@Tax,@Total,@LastUpdated,
                                     @ExpirationOrDueDate,@Taxable,@SalesRepID,@ReferenceNumber,
                                     @ShippingChargeOnOrder,@ShippingChargeOverride,@ShippingServiceID,
                                     @ShippingTrackingNumber,@ShippingNotes,@ReasonCodeID,@ExchangeID,
                                     @ChannelType,@DefaultDiscountReasonCodeID,@DefaultReturnReasonCodeID,
                                     @DefaultTaxChangeReasonCodeID,@SyncGuid);
                                SELECT SCOPE_IDENTITY();", cn, tx))
                            {
                                cmd.CommandTimeout = 60;
                                cmd.Parameters.AddWithValue("@StoreID", request.StoreID);
                                cmd.Parameters.AddWithValue("@Closed", false);
                                cmd.Parameters.AddWithValue("@Time", now);
                                cmd.Parameters.AddWithValue("@Type", QuoteOrderType);
                                cmd.Parameters.AddWithValue("@Comment", request.Comment ?? string.Empty);
                                cmd.Parameters.AddWithValue("@CustomerID", request.CustomerID);
                                cmd.Parameters.AddWithValue("@ShipToID", request.ShipToID);
                                cmd.Parameters.AddWithValue("@DepositOverride", false);
                                cmd.Parameters.AddWithValue("@Deposit", 0m);
                                cmd.Parameters.AddWithValue("@Tax", request.Tax);
                                cmd.Parameters.AddWithValue("@Total", request.Total);
                                cmd.Parameters.AddWithValue("@LastUpdated", now);
                                cmd.Parameters.AddWithValue("@ExpirationOrDueDate", expiration);
                                cmd.Parameters.AddWithValue("@Taxable", request.Taxable);
                                cmd.Parameters.AddWithValue("@SalesRepID", request.SalesRepID);
                                cmd.Parameters.AddWithValue("@ReferenceNumber", SanitizeReferenceNumber(request.ReferenceNumber));
                                cmd.Parameters.AddWithValue("@ShippingChargeOnOrder", 0m);
                                cmd.Parameters.AddWithValue("@ShippingChargeOverride", false);
                                cmd.Parameters.AddWithValue("@ShippingServiceID", 0);
                                cmd.Parameters.AddWithValue("@ShippingTrackingNumber", string.Empty);
                                cmd.Parameters.AddWithValue("@ShippingNotes", string.Empty);
                                cmd.Parameters.AddWithValue("@ReasonCodeID", 0);
                                cmd.Parameters.AddWithValue("@ExchangeID", request.ExchangeID);
                                cmd.Parameters.AddWithValue("@ChannelType", request.ChannelType);
                                cmd.Parameters.AddWithValue("@DefaultDiscountReasonCodeID", request.DefaultDiscountReasonCodeID);
                                cmd.Parameters.AddWithValue("@DefaultReturnReasonCodeID", request.DefaultReturnReasonCodeID);
                                cmd.Parameters.AddWithValue("@DefaultTaxChangeReasonCodeID", request.DefaultTaxChangeReasonCodeID);
                                cmd.Parameters.AddWithValue("@SyncGuid", syncGuid);

                                var result = cmd.ExecuteScalar();
                                orderID = Convert.ToInt32(result);
                            }

                            // INSERT [OrderEntry] por cada ítem
                            foreach (var item in request.Items)
                            {
                                var entrySyncGuid = Guid.NewGuid();
                                var entryTime = DateTime.Now;

                                using (var cmd = new SqlCommand(@"
                                    INSERT INTO [OrderEntry]
                                        ([Cost],[StoreID],[OrderID],[ItemID],[FullPrice],[PriceSource],
                                         [Price],[QuantityOnOrder],[SalesRepID],[Taxable],[DetailID],
                                         [Description],[QuantityRTD],[LastUpdated],[Comment],
                                         [DiscountReasonCodeID],[ReturnReasonCodeID],[TaxChangeReasonCodeID],
                                         [TransactionTime],[IsAddMoney],[VoucherID],[SyncGuid])
                                    VALUES
                                        (@Cost,@StoreID,@OrderID,@ItemID,@FullPrice,@PriceSource,
                                         @Price,@QuantityOnOrder,@SalesRepID,@Taxable,@DetailID,
                                         @Description,@QuantityRTD,@LastUpdated,@Comment,
                                         @DiscountReasonCodeID,@ReturnReasonCodeID,@TaxChangeReasonCodeID,
                                         @TransactionTime,@IsAddMoney,@VoucherID,@SyncGuid);", cn, tx))
                                {
                                    cmd.CommandTimeout = 60;
                                    cmd.Parameters.AddWithValue("@Cost", item.Cost);
                                    cmd.Parameters.AddWithValue("@StoreID", request.StoreID);
                                    cmd.Parameters.AddWithValue("@OrderID", orderID);
                                    cmd.Parameters.AddWithValue("@ItemID", item.ItemID);
                                    cmd.Parameters.AddWithValue("@FullPrice", item.FullPrice);
                                    cmd.Parameters.AddWithValue("@PriceSource", item.PriceSource);
                                    cmd.Parameters.AddWithValue("@Price", item.Price);
                                    cmd.Parameters.AddWithValue("@QuantityOnOrder", Convert.ToDouble(item.QuantityOnOrder));
                                    cmd.Parameters.AddWithValue("@SalesRepID", item.SalesRepID);
                                    cmd.Parameters.AddWithValue("@Taxable", item.Taxable ? 1 : 0);
                                    cmd.Parameters.AddWithValue("@DetailID", item.DetailID);
                                    cmd.Parameters.AddWithValue("@Description", Truncate(item.Description ?? string.Empty, 30));
                                    cmd.Parameters.AddWithValue("@QuantityRTD", 0d);
                                    cmd.Parameters.AddWithValue("@LastUpdated", entryTime);
                                    cmd.Parameters.AddWithValue("@Comment", item.Comment ?? string.Empty);
                                    cmd.Parameters.AddWithValue("@DiscountReasonCodeID", item.DiscountReasonCodeID);
                                    cmd.Parameters.AddWithValue("@ReturnReasonCodeID", item.ReturnReasonCodeID);
                                    cmd.Parameters.AddWithValue("@TaxChangeReasonCodeID", item.TaxChangeReasonCodeID);
                                    cmd.Parameters.AddWithValue("@TransactionTime", entryTime);
                                    cmd.Parameters.AddWithValue("@IsAddMoney", false);
                                    cmd.Parameters.AddWithValue("@VoucherID", 0);
                                    cmd.Parameters.AddWithValue("@SyncGuid", entrySyncGuid);
                                    cmd.ExecuteNonQuery();
                                }
                            }

                            tx.Commit();

                            response.Ok = true;
                            response.OrderID = orderID;
                            response.Tax = request.Tax;
                            response.Total = request.Total;
                            response.Message = "Cotización creada exitosamente.";
                        }
                        catch
                        {
                            tx.Rollback();
                            throw;
                        }
                    }
                }

                return Request.CreateResponse(HttpStatusCode.OK, response);
            }
            catch (SqlException ex)
            {
                response.Ok = false;
                response.Message = "Error de base de datos al crear cotización.";
                LogError(ex);
                return Request.CreateResponse(HttpStatusCode.BadRequest, response);
            }
            catch (Exception ex)
            {
                response.Ok = false;
                response.Message = "Error interno al crear cotización.";
                LogError(ex);
                return Request.CreateResponse(HttpStatusCode.InternalServerError, response);
            }
        }

        [HttpPost]
        [Route("update-quote")]
        public HttpResponseMessage UpdateQuote([FromBody] NovaRetailCreateQuoteRequest request)
        {
            if (request == null || request.OrderID <= 0)
            {
                return Request.CreateResponse(HttpStatusCode.BadRequest, new NovaRetailCreateQuoteResponse
                {
                    Ok = false,
                    Message = "Se requiere un OrderID válido para actualizar."
                });
            }

            if (request.Items == null || request.Items.Count == 0)
            {
                return Request.CreateResponse(HttpStatusCode.BadRequest, new NovaRetailCreateQuoteResponse
                {
                    Ok = false,
                    Message = "La cotización no contiene ítems."
                });
            }

            var response = new NovaRetailCreateQuoteResponse();
            var connectionString = GetConnectionString();

            try
            {
                using (var cn = new SqlConnection(connectionString))
                {
                    cn.Open();

                    using (var tx = cn.BeginTransaction())
                    {
                        try
                        {
                            var now = DateTime.Now;
                            var expiration = request.ExpirationOrDueDate ?? now.AddDays(30);

                            using (var cmd = new SqlCommand(@"
                                UPDATE [Order] SET
                                    [Comment] = @Comment,
                                    [CustomerID] = @CustomerID,
                                    [ShipToID] = @ShipToID,
                                    [Tax] = @Tax,
                                    [Total] = @Total,
                                    [LastUpdated] = @LastUpdated,
                                    [ExpirationOrDueDate] = @ExpirationOrDueDate,
                                    [Taxable] = @Taxable,
                                    [SalesRepID] = @SalesRepID,
                                    [ReferenceNumber] = @ReferenceNumber,
                                    [ExchangeID] = @ExchangeID,
                                    [ChannelType] = @ChannelType,
                                    [DefaultDiscountReasonCodeID] = @DefaultDiscountReasonCodeID,
                                    [DefaultReturnReasonCodeID] = @DefaultReturnReasonCodeID,
                                    [DefaultTaxChangeReasonCodeID] = @DefaultTaxChangeReasonCodeID
                                WHERE ID = @OrderID AND [Type] = @Type AND Closed = 0", cn, tx))
                            {
                                cmd.CommandTimeout = 60;
                                cmd.Parameters.AddWithValue("@OrderID", request.OrderID);
                                cmd.Parameters.AddWithValue("@Type", QuoteOrderType);
                                cmd.Parameters.AddWithValue("@Comment", request.Comment ?? string.Empty);
                                cmd.Parameters.AddWithValue("@CustomerID", request.CustomerID);
                                cmd.Parameters.AddWithValue("@ShipToID", request.ShipToID);
                                cmd.Parameters.AddWithValue("@Tax", request.Tax);
                                cmd.Parameters.AddWithValue("@Total", request.Total);
                                cmd.Parameters.AddWithValue("@LastUpdated", now);
                                cmd.Parameters.AddWithValue("@ExpirationOrDueDate", expiration);
                                cmd.Parameters.AddWithValue("@Taxable", request.Taxable);
                                cmd.Parameters.AddWithValue("@SalesRepID", request.SalesRepID);
                                cmd.Parameters.AddWithValue("@ReferenceNumber", SanitizeReferenceNumber(request.ReferenceNumber));
                                cmd.Parameters.AddWithValue("@ExchangeID", request.ExchangeID);
                                cmd.Parameters.AddWithValue("@ChannelType", request.ChannelType);
                                cmd.Parameters.AddWithValue("@DefaultDiscountReasonCodeID", request.DefaultDiscountReasonCodeID);
                                cmd.Parameters.AddWithValue("@DefaultReturnReasonCodeID", request.DefaultReturnReasonCodeID);
                                cmd.Parameters.AddWithValue("@DefaultTaxChangeReasonCodeID", request.DefaultTaxChangeReasonCodeID);

                                var rowsAffected = cmd.ExecuteNonQuery();
                                if (rowsAffected == 0)
                                {
                                    tx.Rollback();
                                    response.Ok = false;
                                    response.Message = $"No se encontró la orden #{request.OrderID} o ya está cerrada.";
                                    return Request.CreateResponse(HttpStatusCode.BadRequest, response);
                                }
                            }

                            using (var cmd = new SqlCommand("DELETE FROM [OrderEntry] WHERE OrderID = @OrderID", cn, tx))
                            {
                                cmd.CommandTimeout = 60;
                                cmd.Parameters.AddWithValue("@OrderID", request.OrderID);
                                cmd.ExecuteNonQuery();
                            }

                            foreach (var item in request.Items)
                            {
                                var entrySyncGuid = Guid.NewGuid();
                                var entryTime = DateTime.Now;

                                using (var cmd = new SqlCommand(@"
                                    INSERT INTO [OrderEntry]
                                        ([Cost],[StoreID],[OrderID],[ItemID],[FullPrice],[PriceSource],
                                         [Price],[QuantityOnOrder],[SalesRepID],[Taxable],[DetailID],
                                         [Description],[QuantityRTD],[LastUpdated],[Comment],
                                         [DiscountReasonCodeID],[ReturnReasonCodeID],[TaxChangeReasonCodeID],
                                         [TransactionTime],[IsAddMoney],[VoucherID],[SyncGuid])
                                    VALUES
                                        (@Cost,@StoreID,@OrderID,@ItemID,@FullPrice,@PriceSource,
                                         @Price,@QuantityOnOrder,@SalesRepID,@Taxable,@DetailID,
                                         @Description,@QuantityRTD,@LastUpdated,@Comment,
                                         @DiscountReasonCodeID,@ReturnReasonCodeID,@TaxChangeReasonCodeID,
                                         @TransactionTime,@IsAddMoney,@VoucherID,@SyncGuid);", cn, tx))
                                {
                                    cmd.CommandTimeout = 60;
                                    cmd.Parameters.AddWithValue("@Cost", item.Cost);
                                    cmd.Parameters.AddWithValue("@StoreID", request.StoreID);
                                    cmd.Parameters.AddWithValue("@OrderID", request.OrderID);
                                    cmd.Parameters.AddWithValue("@ItemID", item.ItemID);
                                    cmd.Parameters.AddWithValue("@FullPrice", item.FullPrice);
                                    cmd.Parameters.AddWithValue("@PriceSource", item.PriceSource);
                                    cmd.Parameters.AddWithValue("@Price", item.Price);
                                    cmd.Parameters.AddWithValue("@QuantityOnOrder", Convert.ToDouble(item.QuantityOnOrder));
                                    cmd.Parameters.AddWithValue("@SalesRepID", item.SalesRepID);
                                    cmd.Parameters.AddWithValue("@Taxable", item.Taxable ? 1 : 0);
                                    cmd.Parameters.AddWithValue("@DetailID", item.DetailID);
                                    cmd.Parameters.AddWithValue("@Description", Truncate(item.Description ?? string.Empty, 30));
                                    cmd.Parameters.AddWithValue("@QuantityRTD", 0d);
                                    cmd.Parameters.AddWithValue("@LastUpdated", entryTime);
                                    cmd.Parameters.AddWithValue("@Comment", item.Comment ?? string.Empty);
                                    cmd.Parameters.AddWithValue("@DiscountReasonCodeID", item.DiscountReasonCodeID);
                                    cmd.Parameters.AddWithValue("@ReturnReasonCodeID", item.ReturnReasonCodeID);
                                    cmd.Parameters.AddWithValue("@TaxChangeReasonCodeID", item.TaxChangeReasonCodeID);
                                    cmd.Parameters.AddWithValue("@TransactionTime", entryTime);
                                    cmd.Parameters.AddWithValue("@IsAddMoney", false);
                                    cmd.Parameters.AddWithValue("@VoucherID", 0);
                                    cmd.Parameters.AddWithValue("@SyncGuid", entrySyncGuid);
                                    cmd.ExecuteNonQuery();
                                }
                            }

                            tx.Commit();

                            response.Ok = true;
                            response.OrderID = request.OrderID;
                            response.Tax = request.Tax;
                            response.Total = request.Total;
                            response.Message = "Cotización actualizada exitosamente.";
                        }
                        catch
                        {
                            tx.Rollback();
                            throw;
                        }
                    }
                }

                return Request.CreateResponse(HttpStatusCode.OK, response);
            }
            catch (SqlException ex)
            {
                response.Ok = false;
                response.Message = "Error de base de datos al actualizar cotización.";
                LogError(ex);
                return Request.CreateResponse(HttpStatusCode.BadRequest, response);
            }
            catch (Exception ex)
            {
                response.Ok = false;
                response.Message = "Error interno al actualizar cotización.";
                LogError(ex);
                return Request.CreateResponse(HttpStatusCode.InternalServerError, response);
            }
        }

        [HttpPost]
        [Route("create-work-order")]
        public HttpResponseMessage CreateWorkOrder([FromBody] NovaRetailCreateQuoteRequest request)
        {
            if (request == null)
            {
                return Request.CreateResponse(HttpStatusCode.BadRequest, new NovaRetailCreateQuoteResponse
                {
                    Ok = false,
                    Message = "Solicitud inválida."
                });
            }

            if (!ModelState.IsValid)
            {
                var errors = ModelState
                    .Where(x => x.Value != null && x.Value.Errors.Count > 0)
                    .SelectMany(x => x.Value.Errors.Select(e => string.IsNullOrWhiteSpace(x.Key)
                        ? e.ErrorMessage
                        : $"{x.Key}: {e.ErrorMessage}"))
                    .ToList();

                return Request.CreateResponse(HttpStatusCode.BadRequest, new NovaRetailCreateQuoteResponse
                {
                    Ok = false,
                    Message = errors.Count == 0
                        ? "Solicitud inválida."
                        : string.Join(" | ", errors)
                });
            }

            if (request.Items == null || request.Items.Count == 0)
            {
                return Request.CreateResponse(HttpStatusCode.BadRequest, new NovaRetailCreateQuoteResponse
                {
                    Ok = false,
                    Message = "La orden de trabajo no contiene ítems."
                });
            }

            var response = new NovaRetailCreateQuoteResponse();
            var connectionString = GetConnectionString();

            try
            {
                using (var cn = new SqlConnection(connectionString))
                {
                    cn.Open();

                    using (var tx = cn.BeginTransaction())
                    {
                        try
                        {
                            var now = DateTime.Now;
                            var expiration = request.ExpirationOrDueDate ?? now.Date;
                            var syncGuid = Guid.NewGuid();

                            int orderID;
                            using (var cmd = new SqlCommand(@"
                                INSERT INTO [Order]
                                    ([StoreID],[Closed],[Time],[Type],[Comment],[CustomerID],[ShipToID],
                                     [DepositOverride],[Deposit],[Tax],[Total],[LastUpdated],
                                     [ExpirationOrDueDate],[Taxable],[SalesRepID],[ReferenceNumber],
                                     [ShippingChargeOnOrder],[ShippingChargeOverride],[ShippingServiceID],
                                     [ShippingTrackingNumber],[ShippingNotes],[ReasonCodeID],[ExchangeID],
                                     [ChannelType],[DefaultDiscountReasonCodeID],[DefaultReturnReasonCodeID],
                                     [DefaultTaxChangeReasonCodeID],[SyncGuid])
                                VALUES
                                    (@StoreID,@Closed,@Time,@Type,@Comment,@CustomerID,@ShipToID,
                                     @DepositOverride,@Deposit,@Tax,@Total,@LastUpdated,
                                     @ExpirationOrDueDate,@Taxable,@SalesRepID,@ReferenceNumber,
                                     @ShippingChargeOnOrder,@ShippingChargeOverride,@ShippingServiceID,
                                     @ShippingTrackingNumber,@ShippingNotes,@ReasonCodeID,@ExchangeID,
                                     @ChannelType,@DefaultDiscountReasonCodeID,@DefaultReturnReasonCodeID,
                                     @DefaultTaxChangeReasonCodeID,@SyncGuid);
                                SELECT SCOPE_IDENTITY();", cn, tx))
                            {
                                cmd.CommandTimeout = 60;
                                cmd.Parameters.AddWithValue("@StoreID", request.StoreID);
                                cmd.Parameters.AddWithValue("@Closed", false);
                                cmd.Parameters.AddWithValue("@Time", now);
                                cmd.Parameters.AddWithValue("@Type", WorkOrderType);
                                cmd.Parameters.AddWithValue("@Comment", request.Comment ?? string.Empty);
                                cmd.Parameters.AddWithValue("@CustomerID", request.CustomerID);
                                cmd.Parameters.AddWithValue("@ShipToID", request.ShipToID);
                                cmd.Parameters.AddWithValue("@DepositOverride", false);
                                cmd.Parameters.AddWithValue("@Deposit", 0m);
                                cmd.Parameters.AddWithValue("@Tax", request.Tax);
                                cmd.Parameters.AddWithValue("@Total", request.Total);
                                cmd.Parameters.AddWithValue("@LastUpdated", now);
                                cmd.Parameters.AddWithValue("@ExpirationOrDueDate", expiration);
                                cmd.Parameters.AddWithValue("@Taxable", request.Taxable);
                                cmd.Parameters.AddWithValue("@SalesRepID", request.SalesRepID);
                                cmd.Parameters.AddWithValue("@ReferenceNumber", SanitizeReferenceNumber(request.ReferenceNumber));
                                cmd.Parameters.AddWithValue("@ShippingChargeOnOrder", 0m);
                                cmd.Parameters.AddWithValue("@ShippingChargeOverride", false);
                                cmd.Parameters.AddWithValue("@ShippingServiceID", 0);
                                cmd.Parameters.AddWithValue("@ShippingTrackingNumber", string.Empty);
                                cmd.Parameters.AddWithValue("@ShippingNotes", string.Empty);
                                cmd.Parameters.AddWithValue("@ReasonCodeID", 0);
                                cmd.Parameters.AddWithValue("@ExchangeID", request.ExchangeID);
                                cmd.Parameters.AddWithValue("@ChannelType", request.ChannelType);
                                cmd.Parameters.AddWithValue("@DefaultDiscountReasonCodeID", request.DefaultDiscountReasonCodeID);
                                cmd.Parameters.AddWithValue("@DefaultReturnReasonCodeID", request.DefaultReturnReasonCodeID);
                                cmd.Parameters.AddWithValue("@DefaultTaxChangeReasonCodeID", request.DefaultTaxChangeReasonCodeID);
                                cmd.Parameters.AddWithValue("@SyncGuid", syncGuid);

                                orderID = Convert.ToInt32(cmd.ExecuteScalar());
                            }

                            foreach (var item in request.Items)
                            {
                                var entrySyncGuid = Guid.NewGuid();
                                var entryTime = DateTime.Now;

                                using (var cmd = new SqlCommand(@"
                                    INSERT INTO [OrderEntry]
                                        ([Cost],[StoreID],[OrderID],[ItemID],[FullPrice],[PriceSource],
                                         [Price],[QuantityOnOrder],[SalesRepID],[Taxable],[DetailID],
                                         [Description],[QuantityRTD],[LastUpdated],[Comment],
                                         [DiscountReasonCodeID],[ReturnReasonCodeID],[TaxChangeReasonCodeID],
                                         [TransactionTime],[IsAddMoney],[VoucherID],[SyncGuid])
                                    VALUES
                                        (@Cost,@StoreID,@OrderID,@ItemID,@FullPrice,@PriceSource,
                                         @Price,@QuantityOnOrder,@SalesRepID,@Taxable,@DetailID,
                                         @Description,@QuantityRTD,@LastUpdated,@Comment,
                                         @DiscountReasonCodeID,@ReturnReasonCodeID,@TaxChangeReasonCodeID,
                                         @TransactionTime,@IsAddMoney,@VoucherID,@SyncGuid);", cn, tx))
                                {
                                    cmd.CommandTimeout = 60;
                                    cmd.Parameters.AddWithValue("@Cost", item.Cost);
                                    cmd.Parameters.AddWithValue("@StoreID", request.StoreID);
                                    cmd.Parameters.AddWithValue("@OrderID", orderID);
                                    cmd.Parameters.AddWithValue("@ItemID", item.ItemID);
                                    cmd.Parameters.AddWithValue("@FullPrice", item.FullPrice);
                                    cmd.Parameters.AddWithValue("@PriceSource", item.PriceSource);
                                    cmd.Parameters.AddWithValue("@Price", item.Price);
                                    cmd.Parameters.AddWithValue("@QuantityOnOrder", Convert.ToDouble(item.QuantityOnOrder));
                                    cmd.Parameters.AddWithValue("@SalesRepID", item.SalesRepID);
                                    cmd.Parameters.AddWithValue("@Taxable", item.Taxable ? 1 : 0);
                                    cmd.Parameters.AddWithValue("@DetailID", item.DetailID);
                                    cmd.Parameters.AddWithValue("@Description", Truncate(item.Description ?? string.Empty, 30));
                                    cmd.Parameters.AddWithValue("@QuantityRTD", 0d);
                                    cmd.Parameters.AddWithValue("@LastUpdated", entryTime);
                                    cmd.Parameters.AddWithValue("@Comment", item.Comment ?? string.Empty);
                                    cmd.Parameters.AddWithValue("@DiscountReasonCodeID", item.DiscountReasonCodeID);
                                    cmd.Parameters.AddWithValue("@ReturnReasonCodeID", item.ReturnReasonCodeID);
                                    cmd.Parameters.AddWithValue("@TaxChangeReasonCodeID", item.TaxChangeReasonCodeID);
                                    cmd.Parameters.AddWithValue("@TransactionTime", entryTime);
                                    cmd.Parameters.AddWithValue("@IsAddMoney", false);
                                    cmd.Parameters.AddWithValue("@VoucherID", 0);
                                    cmd.Parameters.AddWithValue("@SyncGuid", entrySyncGuid);
                                    cmd.ExecuteNonQuery();
                                }
                            }

                            ReserveWorkOrderInventory(cn, request.Items, tx);

                            tx.Commit();

                            response.Ok = true;
                            response.OrderID = orderID;
                            response.Tax = request.Tax;
                            response.Total = request.Total;
                            response.Message = "Orden de trabajo creada exitosamente.";
                        }
                        catch
                        {
                            tx.Rollback();
                            throw;
                        }
                    }
                }

                return Request.CreateResponse(HttpStatusCode.OK, response);
            }
            catch (SqlException ex)
            {
                response.Ok = false;
                response.Message = "Error de base de datos al crear orden de trabajo.";
                LogError(ex);
                return Request.CreateResponse(HttpStatusCode.BadRequest, response);
            }
            catch (Exception ex)
            {
                response.Ok = false;
                response.Message = "Error interno al crear orden de trabajo.";
                LogError(ex);
                return Request.CreateResponse(HttpStatusCode.InternalServerError, response);
            }
        }

        [HttpPost]
        [Route("update-work-order")]
        public HttpResponseMessage UpdateWorkOrder([FromBody] NovaRetailCreateQuoteRequest request)
        {
            if (request == null || request.OrderID <= 0)
            {
                return Request.CreateResponse(HttpStatusCode.BadRequest, new NovaRetailCreateQuoteResponse
                {
                    Ok = false,
                    Message = "Se requiere un OrderID válido para actualizar."
                });
            }

            if (request.Items == null || request.Items.Count == 0)
            {
                return Request.CreateResponse(HttpStatusCode.BadRequest, new NovaRetailCreateQuoteResponse
                {
                    Ok = false,
                    Message = "La orden de trabajo no contiene ítems."
                });
            }

            var response = new NovaRetailCreateQuoteResponse();
            var connectionString = GetConnectionString();

            try
            {
                using (var cn = new SqlConnection(connectionString))
                {
                    cn.Open();

                    using (var tx = cn.BeginTransaction())
                    {
                        try
                        {
                            var now = DateTime.Now;
                            var expiration = request.ExpirationOrDueDate ?? now.Date;

                            using (var cmd = new SqlCommand(@"
                                UPDATE [Order] SET
                                    [Comment] = @Comment,
                                    [CustomerID] = @CustomerID,
                                    [ShipToID] = @ShipToID,
                                    [Tax] = @Tax,
                                    [Total] = @Total,
                                    [LastUpdated] = @LastUpdated,
                                    [ExpirationOrDueDate] = @ExpirationOrDueDate,
                                    [Taxable] = @Taxable,
                                    [SalesRepID] = @SalesRepID,
                                    [ReferenceNumber] = @ReferenceNumber,
                                    [ExchangeID] = @ExchangeID,
                                    [ChannelType] = @ChannelType,
                                    [DefaultDiscountReasonCodeID] = @DefaultDiscountReasonCodeID,
                                    [DefaultReturnReasonCodeID] = @DefaultReturnReasonCodeID,
                                    [DefaultTaxChangeReasonCodeID] = @DefaultTaxChangeReasonCodeID
                                WHERE ID = @OrderID AND [Type] = @Type AND Closed = 0", cn, tx))
                            {
                                cmd.CommandTimeout = 60;
                                cmd.Parameters.AddWithValue("@OrderID", request.OrderID);
                                cmd.Parameters.AddWithValue("@Type", WorkOrderType);
                                cmd.Parameters.AddWithValue("@Comment", request.Comment ?? string.Empty);
                                cmd.Parameters.AddWithValue("@CustomerID", request.CustomerID);
                                cmd.Parameters.AddWithValue("@ShipToID", request.ShipToID);
                                cmd.Parameters.AddWithValue("@Tax", request.Tax);
                                cmd.Parameters.AddWithValue("@Total", request.Total);
                                cmd.Parameters.AddWithValue("@LastUpdated", now);
                                cmd.Parameters.AddWithValue("@ExpirationOrDueDate", expiration);
                                cmd.Parameters.AddWithValue("@Taxable", request.Taxable);
                                cmd.Parameters.AddWithValue("@SalesRepID", request.SalesRepID);
                                cmd.Parameters.AddWithValue("@ReferenceNumber", SanitizeReferenceNumber(request.ReferenceNumber));
                                cmd.Parameters.AddWithValue("@ExchangeID", request.ExchangeID);
                                cmd.Parameters.AddWithValue("@ChannelType", request.ChannelType);
                                cmd.Parameters.AddWithValue("@DefaultDiscountReasonCodeID", request.DefaultDiscountReasonCodeID);
                                cmd.Parameters.AddWithValue("@DefaultReturnReasonCodeID", request.DefaultReturnReasonCodeID);
                                cmd.Parameters.AddWithValue("@DefaultTaxChangeReasonCodeID", request.DefaultTaxChangeReasonCodeID);

                                var rowsAffected = cmd.ExecuteNonQuery();
                                if (rowsAffected == 0)
                                {
                                    tx.Rollback();
                                    response.Ok = false;
                                    response.Message = $"No se encontró la orden de trabajo #{request.OrderID} o ya está cerrada.";
                                    return Request.CreateResponse(HttpStatusCode.BadRequest, response);
                                }
                            }

                            ReleaseWorkOrderInventory(cn, request.OrderID, tx);

                            using (var cmd = new SqlCommand("DELETE FROM [OrderEntry] WHERE OrderID = @OrderID", cn, tx))
                            {
                                cmd.CommandTimeout = 60;
                                cmd.Parameters.AddWithValue("@OrderID", request.OrderID);
                                cmd.ExecuteNonQuery();
                            }

                            foreach (var item in request.Items)
                            {
                                var entrySyncGuid = Guid.NewGuid();
                                var entryTime = DateTime.Now;

                                using (var cmd = new SqlCommand(@"
                                    INSERT INTO [OrderEntry]
                                        ([Cost],[StoreID],[OrderID],[ItemID],[FullPrice],[PriceSource],
                                         [Price],[QuantityOnOrder],[SalesRepID],[Taxable],[DetailID],
                                         [Description],[QuantityRTD],[LastUpdated],[Comment],
                                         [DiscountReasonCodeID],[ReturnReasonCodeID],[TaxChangeReasonCodeID],
                                         [TransactionTime],[IsAddMoney],[VoucherID],[SyncGuid])
                                    VALUES
                                        (@Cost,@StoreID,@OrderID,@ItemID,@FullPrice,@PriceSource,
                                         @Price,@QuantityOnOrder,@SalesRepID,@Taxable,@DetailID,
                                         @Description,@QuantityRTD,@LastUpdated,@Comment,
                                         @DiscountReasonCodeID,@ReturnReasonCodeID,@TaxChangeReasonCodeID,
                                         @TransactionTime,@IsAddMoney,@VoucherID,@SyncGuid);", cn, tx))
                                {
                                    cmd.CommandTimeout = 60;
                                    cmd.Parameters.AddWithValue("@Cost", item.Cost);
                                    cmd.Parameters.AddWithValue("@StoreID", request.StoreID);
                                    cmd.Parameters.AddWithValue("@OrderID", request.OrderID);
                                    cmd.Parameters.AddWithValue("@ItemID", item.ItemID);
                                    cmd.Parameters.AddWithValue("@FullPrice", item.FullPrice);
                                    cmd.Parameters.AddWithValue("@PriceSource", item.PriceSource);
                                    cmd.Parameters.AddWithValue("@Price", item.Price);
                                    cmd.Parameters.AddWithValue("@QuantityOnOrder", Convert.ToDouble(item.QuantityOnOrder));
                                    cmd.Parameters.AddWithValue("@SalesRepID", item.SalesRepID);
                                    cmd.Parameters.AddWithValue("@Taxable", item.Taxable ? 1 : 0);
                                    cmd.Parameters.AddWithValue("@DetailID", item.DetailID);
                                    cmd.Parameters.AddWithValue("@Description", Truncate(item.Description ?? string.Empty, 30));
                                    cmd.Parameters.AddWithValue("@QuantityRTD", 0d);
                                    cmd.Parameters.AddWithValue("@LastUpdated", entryTime);
                                    cmd.Parameters.AddWithValue("@Comment", item.Comment ?? string.Empty);
                                    cmd.Parameters.AddWithValue("@DiscountReasonCodeID", item.DiscountReasonCodeID);
                                    cmd.Parameters.AddWithValue("@ReturnReasonCodeID", item.ReturnReasonCodeID);
                                    cmd.Parameters.AddWithValue("@TaxChangeReasonCodeID", item.TaxChangeReasonCodeID);
                                    cmd.Parameters.AddWithValue("@TransactionTime", entryTime);
                                    cmd.Parameters.AddWithValue("@IsAddMoney", false);
                                    cmd.Parameters.AddWithValue("@VoucherID", 0);
                                    cmd.Parameters.AddWithValue("@SyncGuid", entrySyncGuid);
                                    cmd.ExecuteNonQuery();
                                }
                            }

                            ReserveWorkOrderInventory(cn, request.Items, tx);

                            tx.Commit();

                            response.Ok = true;
                            response.OrderID = request.OrderID;
                            response.Tax = request.Tax;
                            response.Total = request.Total;
                            response.Message = "Orden de trabajo actualizada exitosamente.";
                        }
                        catch
                        {
                            tx.Rollback();
                            throw;
                        }
                    }
                }

                return Request.CreateResponse(HttpStatusCode.OK, response);
            }
            catch (SqlException ex)
            {
                response.Ok = false;
                response.Message = "Error de base de datos al actualizar orden de trabajo.";
                LogError(ex);
                return Request.CreateResponse(HttpStatusCode.BadRequest, response);
            }
            catch (Exception ex)
            {
                response.Ok = false;
                response.Message = "Error interno al actualizar orden de trabajo.";
                LogError(ex);
                return Request.CreateResponse(HttpStatusCode.InternalServerError, response);
            }
        }

        private static void DeleteTransactionHold(SqlConnection cn, int holdId)
        {
            using (var cmd = new SqlCommand(
                "DELETE FROM dbo.TransactionHoldEntry WHERE TransactionHoldID = @HoldID; " +
                "DELETE FROM dbo.TransactionHold WHERE ID = @HoldID;", cn))
            {
                cmd.CommandTimeout = 30;
                cmd.Parameters.AddWithValue("@HoldID", holdId);
                cmd.ExecuteNonQuery();
            }
        }

        private sealed class OrderItemCommitment
        {
            public int ItemID { get; set; }
            public double Quantity { get; set; }
        }

        private static List<OrderItemCommitment> BuildOrderItemCommitments(IEnumerable<NovaRetailQuoteItemDto> items)
        {
            if (items == null)
                return new List<OrderItemCommitment>();

            return items
                .Where(item => item != null && item.ItemID > 0 && item.QuantityOnOrder > 0)
                .GroupBy(item => item.ItemID)
                .Select(group => new OrderItemCommitment
                {
                    ItemID = group.Key,
                    Quantity = group.Sum(item => Convert.ToDouble(item.QuantityOnOrder))
                })
                .Where(item => item.Quantity > 0d)
                .ToList();
        }

        private static List<OrderItemCommitment> LoadOrderItemCommitments(SqlConnection cn, int orderId, SqlTransaction tx = null)
        {
            var commitments = new List<OrderItemCommitment>();

            using (var cmd = new SqlCommand(@"
                SELECT oe.ItemID,
                       SUM(CAST(ISNULL(oe.QuantityOnOrder, 0) AS float)) AS QuantityOnOrder
                FROM dbo.OrderEntry oe
                WHERE oe.OrderID = @OrderID
                GROUP BY oe.ItemID", cn))
            {
                if (tx != null)
                    cmd.Transaction = tx;

                cmd.CommandTimeout = 30;
                cmd.Parameters.AddWithValue("@OrderID", orderId);

                using (var reader = cmd.ExecuteReader())
                {
                    while (reader.Read())
                    {
                        commitments.Add(new OrderItemCommitment
                        {
                            ItemID = Convert.ToInt32(reader["ItemID"]),
                            Quantity = reader["QuantityOnOrder"] == DBNull.Value
                                ? 0d
                                : Convert.ToDouble(reader["QuantityOnOrder"])
                        });
                    }
                }
            }

            return commitments;
        }

        private static void ApplyCommittedInventoryDelta(SqlConnection cn, IEnumerable<OrderItemCommitment> commitments, int direction, SqlTransaction tx = null)
        {
            if (commitments == null)
                return;

            if (direction != 1 && direction != -1)
                throw new ArgumentOutOfRangeException(nameof(direction));

            foreach (var commitment in commitments)
            {
                if (commitment == null || commitment.ItemID <= 0 || commitment.Quantity <= 0d)
                    continue;

                using (var cmd = new SqlCommand(@"
                    UPDATE dbo.Item
                    SET [Quantity] = [Quantity] - 0,
                        [QuantityCommitted] = CASE
                            WHEN ISNULL([QuantityCommitted], 0) + @QuantityDelta < 0 THEN 0
                            ELSE ISNULL([QuantityCommitted], 0) + @QuantityDelta
                        END,
                        [BuydownQuantity] = [BuydownQuantity] - 0,
                        [LastUpdated] = GETDATE()
                    WHERE [ID] = @ItemID", cn))
                {
                    if (tx != null)
                        cmd.Transaction = tx;

                    cmd.CommandTimeout = 30;
                    cmd.Parameters.AddWithValue("@ItemID", commitment.ItemID);
                    cmd.Parameters.AddWithValue("@QuantityDelta", commitment.Quantity * direction);
                    cmd.ExecuteNonQuery();
                }
            }
        }

        private static void ReserveWorkOrderInventory(SqlConnection cn, IEnumerable<NovaRetailQuoteItemDto> items, SqlTransaction tx = null)
        {
            ApplyCommittedInventoryDelta(cn, BuildOrderItemCommitments(items), 1, tx);
        }

        private static void ReleaseWorkOrderInventory(SqlConnection cn, int orderId, SqlTransaction tx = null)
        {
            ApplyCommittedInventoryDelta(cn, LoadOrderItemCommitments(cn, orderId, tx), -1, tx);
        }

        private static int LoadOrderType(SqlConnection cn, int orderId, SqlTransaction tx = null)
        {
            using (var cmd = new SqlCommand("SELECT TOP 1 [Type] FROM dbo.[Order] WHERE ID = @OrderID", cn))
            {
                if (tx != null)
                    cmd.Transaction = tx;

                cmd.CommandTimeout = 30;
                cmd.Parameters.AddWithValue("@OrderID", orderId);

                var result = cmd.ExecuteScalar();
                return result == null || result == DBNull.Value
                    ? 0
                    : Convert.ToInt32(result);
            }
        }

        private static int CloseOrder(SqlConnection cn, int orderId, int orderType, SqlTransaction tx = null)
        {
            using (var cmd = new SqlCommand(@"
                UPDATE dbo.[Order]
                SET Closed = 1,
                    LastUpdated = @LastUpdated
                WHERE ID = @OrderID
                  AND [Type] = @Type
                  AND Closed = 0", cn))
            {
                if (tx != null)
                    cmd.Transaction = tx;

                cmd.CommandTimeout = 30;
                cmd.Parameters.AddWithValue("@OrderID", orderId);
                cmd.Parameters.AddWithValue("@Type", orderType);
                cmd.Parameters.AddWithValue("@LastUpdated", DateTime.Now);
                return cmd.ExecuteNonQuery();
            }
        }

        private static int CloseQuoteOrder(SqlConnection cn, int orderId, SqlTransaction tx = null)
            => CloseOrder(cn, orderId, QuoteOrderType, tx);

        private static int CloseWorkOrder(SqlConnection cn, int orderId, SqlTransaction tx = null)
        {
            var rowsAffected = CloseOrder(cn, orderId, WorkOrderType, tx);
            if (rowsAffected > 0)
                ReleaseWorkOrderInventory(cn, orderId, tx);

            return rowsAffected;
        }

        [HttpPost]
        [Route("save-hold")]
        public HttpResponseMessage SaveHold([FromBody] NovaRetailCreateQuoteRequest request)
        {
            if (request == null || request.Items == null || request.Items.Count == 0)
                return Request.CreateResponse(HttpStatusCode.BadRequest, new NovaRetailCreateQuoteResponse { Ok = false, Message = "La factura en espera no contiene ítems." });

            var response = new NovaRetailCreateQuoteResponse();
            var connectionString = GetConnectionString();
            try
            {
                using (var cn = new SqlConnection(connectionString))
                {
                    cn.Open();
                    using (var tx = cn.BeginTransaction())
                    {
                        try
                        {
                            var now = DateTime.Now;
                            var expiration = request.ExpirationOrDueDate ?? now.AddDays(1);
                            var syncGuid = Guid.NewGuid();
                            var activeBatch = ResolveActiveBatch(cn, request.StoreID, 0, tx);
                            var batchNumber = activeBatch?.BatchNumber ?? 0;

                            int holdId;
                            using (var cmd = new SqlCommand(@"
                                INSERT INTO [TransactionHold]
                                    ([StoreID],[TransactionType],[HoldComment],[RecallID],[Comment],[PriceLevel],
                                     [DiscountMethod],[DiscountPercent],[Taxable],[CustomerID],[DeltaDeposit],
                                     [DepositOverride],[DepositPrevious],[PaymentsPrevious],[TaxPrevious],[SalesRepID],
                                     [ShipToID],[TransactionTime],[ExpirationOrDueDate],[ReturnMode],[ReferenceNumber],
                                     [ShippingChargePurchased],[ShippingChargeOverride],[ShippingServiceID],
                                     [ShippingTrackingNumber],[ShippingNotes],[ReasonCodeID],[ExchangeID],[ChannelType],
                                     [DefaultDiscountReasonCodeID],[DefaultReturnReasonCodeID],[DefaultTaxChangeReasonCodeID],
                                     [BatchNumber],[SyncGuid])
                                VALUES
                                    (@StoreID,1,@HoldComment,0,'',1,0,0,@Taxable,@CustomerID,0,0,0,0,0,
                                     @SalesRepID,@ShipToID,@Time,@Expiration,0,@ReferenceNumber,0,0,0,'','',0,
                                     @ExchangeID,@ChannelType,@DefaultDiscountReasonCodeID,@DefaultReturnReasonCodeID,
                                     @DefaultTaxChangeReasonCodeID,@BatchNumber,@SyncGuid);
                                SELECT SCOPE_IDENTITY();", cn, tx))
                            {
                                cmd.CommandTimeout = 60;
                                cmd.Parameters.AddWithValue("@StoreID", request.StoreID);
                                cmd.Parameters.AddWithValue("@HoldComment", request.Comment ?? string.Empty);
                                cmd.Parameters.AddWithValue("@Taxable", request.Taxable);
                                cmd.Parameters.AddWithValue("@CustomerID", request.CustomerID);
                                cmd.Parameters.AddWithValue("@SalesRepID", request.SalesRepID);
                                cmd.Parameters.AddWithValue("@ShipToID", request.ShipToID);
                                cmd.Parameters.AddWithValue("@Time", now);
                                cmd.Parameters.AddWithValue("@Expiration", expiration);
                                cmd.Parameters.AddWithValue("@ReferenceNumber", SanitizeReferenceNumber(request.ReferenceNumber));
                                cmd.Parameters.AddWithValue("@ExchangeID", request.ExchangeID);
                                cmd.Parameters.AddWithValue("@ChannelType", request.ChannelType);
                                cmd.Parameters.AddWithValue("@DefaultDiscountReasonCodeID", request.DefaultDiscountReasonCodeID);
                                cmd.Parameters.AddWithValue("@DefaultReturnReasonCodeID", request.DefaultReturnReasonCodeID);
                                cmd.Parameters.AddWithValue("@DefaultTaxChangeReasonCodeID", request.DefaultTaxChangeReasonCodeID);
                                cmd.Parameters.AddWithValue("@BatchNumber", batchNumber);
                                cmd.Parameters.AddWithValue("@SyncGuid", syncGuid);
                                holdId = Convert.ToInt32(cmd.ExecuteScalar());
                            }

                            foreach (var item in request.Items)
                            {
                                var entryTime = DateTime.Now;
                                var entrySyncGuid = Guid.NewGuid();
                                using (var cmd = new SqlCommand(@"
                                    INSERT INTO [TransactionHoldEntry]
                                        ([EntryKey],[StoreID],[TransactionHoldID],[RecallID],[Description],
                                         [QuantityPurchased],[QuantityOnOrder],[QuantityRTD],[QuantityReserved],
                                         [Price],[FullPrice],[PriceSource],[Comment],[DetailID],[Taxable],[ItemID],
                                         [SalesRepID],[SerialNumber1],[SerialNumber2],[SerialNumber3],[VoucherNumber],
                                         [VoucherExpirationDate],[DiscountReasonCodeID],[ReturnReasonCodeID],
                                         [TaxChangeReasonCodeID],[ItemTaxID],[ComponentQuantityReserved],
                                         [TransactionTime],[IsAddMoney],[VoucherID],[SyncGuid])
                                    VALUES
                                        ('',@StoreID,@HoldID,0,@Description,
                                         0,0,0,@QuantityReserved,
                                         @Price,@FullPrice,@PriceSource,@Comment,@DetailID,@Taxable,@ItemID,
                                         @SalesRepID,'','','','',
                                         NULL,@DiscountReasonCodeID,@ReturnReasonCodeID,
                                         @TaxChangeReasonCodeID,
                                         ISNULL((SELECT TOP 1 TaxID FROM dbo.Item WHERE ID=@ItemID),0),0,
                                         @TransactionTime,0,0,@SyncGuid);", cn, tx))
                                {
                                    cmd.CommandTimeout = 60;
                                    cmd.Parameters.AddWithValue("@StoreID", request.StoreID);
                                    cmd.Parameters.AddWithValue("@HoldID", holdId);
                                    cmd.Parameters.AddWithValue("@Description", Truncate(item.Description ?? string.Empty, 80));
                                    cmd.Parameters.AddWithValue("@QuantityReserved", Convert.ToDouble(item.QuantityOnOrder));
                                    cmd.Parameters.AddWithValue("@Price", item.Price);
                                    cmd.Parameters.AddWithValue("@FullPrice", item.FullPrice);
                                    cmd.Parameters.AddWithValue("@PriceSource", item.PriceSource);
                                    cmd.Parameters.AddWithValue("@Comment", item.Comment ?? string.Empty);
                                    cmd.Parameters.AddWithValue("@DetailID", item.DetailID);
                                    cmd.Parameters.AddWithValue("@Taxable", item.Taxable ? 1 : 0);
                                    cmd.Parameters.AddWithValue("@ItemID", item.ItemID);
                                    cmd.Parameters.AddWithValue("@SalesRepID", item.SalesRepID);
                                    cmd.Parameters.AddWithValue("@DiscountReasonCodeID", item.DiscountReasonCodeID);
                                    cmd.Parameters.AddWithValue("@ReturnReasonCodeID", item.ReturnReasonCodeID);
                                    cmd.Parameters.AddWithValue("@TaxChangeReasonCodeID", item.TaxChangeReasonCodeID);
                                    cmd.Parameters.AddWithValue("@TransactionTime", entryTime);
                                    cmd.Parameters.AddWithValue("@SyncGuid", entrySyncGuid);
                                    cmd.ExecuteNonQuery();
                                }
                            }

                            tx.Commit();
                            response.Ok = true;
                            response.OrderID = holdId;
                            response.Tax = request.Tax;
                            response.Total = request.Total;
                            response.Message = "Factura en espera guardada exitosamente.";
                        }
                        catch
                        {
                            tx.Rollback();
                            throw;
                        }
                    }
                }
                return Request.CreateResponse(HttpStatusCode.OK, response);
            }
            catch (SqlException ex)
            {
                response.Ok = false;
                response.Message = "Error de base de datos al crear factura en espera.";
                LogError(ex);
                return Request.CreateResponse(HttpStatusCode.BadRequest, response);
            }
            catch (Exception ex)
            {
                response.Ok = false;
                response.Message = "Error interno al crear factura en espera.";
                LogError(ex);
                return Request.CreateResponse(HttpStatusCode.InternalServerError, response);
            }
        }

        [HttpPost]
        [Route("update-hold")]
        public HttpResponseMessage UpdateHold([FromBody] NovaRetailCreateQuoteRequest request)
        {
            if (request == null || request.OrderID <= 0)
                return Request.CreateResponse(HttpStatusCode.BadRequest, new NovaRetailCreateQuoteResponse { Ok = false, Message = "Se requiere un HoldID válido para actualizar." });

            if (request.Items == null || request.Items.Count == 0)
                return Request.CreateResponse(HttpStatusCode.BadRequest, new NovaRetailCreateQuoteResponse { Ok = false, Message = "La factura en espera no contiene ítems." });

            var response = new NovaRetailCreateQuoteResponse();
            var connectionString = GetConnectionString();
            try
            {
                using (var cn = new SqlConnection(connectionString))
                {
                    cn.Open();
                    using (var tx = cn.BeginTransaction())
                    {
                        try
                        {
                            var now = DateTime.Now;
                            var expiration = request.ExpirationOrDueDate ?? now.AddDays(1);

                            using (var cmd = new SqlCommand(@"
                                UPDATE [TransactionHold] SET
                                    [HoldComment] = @HoldComment,
                                    [ReferenceNumber] = @ReferenceNumber,
                                    [ExpirationOrDueDate] = @Expiration
                                WHERE ID = @HoldID", cn, tx))
                            {
                                cmd.CommandTimeout = 60;
                                cmd.Parameters.AddWithValue("@HoldID", request.OrderID);
                                cmd.Parameters.AddWithValue("@HoldComment", request.Comment ?? string.Empty);
                                cmd.Parameters.AddWithValue("@ReferenceNumber", SanitizeReferenceNumber(request.ReferenceNumber));
                                cmd.Parameters.AddWithValue("@Expiration", expiration);
                                var rows = cmd.ExecuteNonQuery();
                                if (rows == 0)
                                {
                                    tx.Rollback();
                                    response.Ok = false;
                                    response.Message = $"No se encontró la factura en espera #{request.OrderID}.";
                                    return Request.CreateResponse(HttpStatusCode.BadRequest, response);
                                }
                            }

                            using (var cmd = new SqlCommand("DELETE FROM [TransactionHoldEntry] WHERE TransactionHoldID = @HoldID", cn, tx))
                            {
                                cmd.CommandTimeout = 60;
                                cmd.Parameters.AddWithValue("@HoldID", request.OrderID);
                                cmd.ExecuteNonQuery();
                            }

                            foreach (var item in request.Items)
                            {
                                var entryTime = DateTime.Now;
                                var entrySyncGuid = Guid.NewGuid();
                                using (var cmd = new SqlCommand(@"
                                    INSERT INTO [TransactionHoldEntry]
                                        ([EntryKey],[StoreID],[TransactionHoldID],[RecallID],[Description],
                                         [QuantityPurchased],[QuantityOnOrder],[QuantityRTD],[QuantityReserved],
                                         [Price],[FullPrice],[PriceSource],[Comment],[DetailID],[Taxable],[ItemID],
                                         [SalesRepID],[SerialNumber1],[SerialNumber2],[SerialNumber3],[VoucherNumber],
                                         [VoucherExpirationDate],[DiscountReasonCodeID],[ReturnReasonCodeID],
                                         [TaxChangeReasonCodeID],[ItemTaxID],[ComponentQuantityReserved],
                                         [TransactionTime],[IsAddMoney],[VoucherID],[SyncGuid])
                                    VALUES
                                        ('',@StoreID,@HoldID,0,@Description,
                                         0,0,0,@QuantityReserved,
                                         @Price,@FullPrice,@PriceSource,@Comment,@DetailID,@Taxable,@ItemID,
                                         @SalesRepID,'','','','',
                                         NULL,@DiscountReasonCodeID,@ReturnReasonCodeID,
                                         @TaxChangeReasonCodeID,
                                         ISNULL((SELECT TOP 1 TaxID FROM dbo.Item WHERE ID=@ItemID),0),0,
                                         @TransactionTime,0,0,@SyncGuid);", cn, tx))
                                {
                                    cmd.CommandTimeout = 60;
                                    cmd.Parameters.AddWithValue("@StoreID", request.StoreID);
                                    cmd.Parameters.AddWithValue("@HoldID", request.OrderID);
                                    cmd.Parameters.AddWithValue("@Description", Truncate(item.Description ?? string.Empty, 80));
                                    cmd.Parameters.AddWithValue("@QuantityReserved", Convert.ToDouble(item.QuantityOnOrder));
                                    cmd.Parameters.AddWithValue("@Price", item.Price);
                                    cmd.Parameters.AddWithValue("@FullPrice", item.FullPrice);
                                    cmd.Parameters.AddWithValue("@PriceSource", item.PriceSource);
                                    cmd.Parameters.AddWithValue("@Comment", item.Comment ?? string.Empty);
                                    cmd.Parameters.AddWithValue("@DetailID", item.DetailID);
                                    cmd.Parameters.AddWithValue("@Taxable", item.Taxable ? 1 : 0);
                                    cmd.Parameters.AddWithValue("@ItemID", item.ItemID);
                                    cmd.Parameters.AddWithValue("@SalesRepID", item.SalesRepID);
                                    cmd.Parameters.AddWithValue("@DiscountReasonCodeID", item.DiscountReasonCodeID);
                                    cmd.Parameters.AddWithValue("@ReturnReasonCodeID", item.ReturnReasonCodeID);
                                    cmd.Parameters.AddWithValue("@TaxChangeReasonCodeID", item.TaxChangeReasonCodeID);
                                    cmd.Parameters.AddWithValue("@TransactionTime", entryTime);
                                    cmd.Parameters.AddWithValue("@SyncGuid", entrySyncGuid);
                                    cmd.ExecuteNonQuery();
                                }
                            }

                            tx.Commit();
                            response.Ok = true;
                            response.OrderID = request.OrderID;
                            response.Message = "Factura en espera actualizada exitosamente.";
                        }
                        catch
                        {
                            tx.Rollback();
                            throw;
                        }
                    }
                }
                return Request.CreateResponse(HttpStatusCode.OK, response);
            }
            catch (SqlException ex)
            {
                response.Ok = false;
                response.Message = "Error de base de datos al actualizar factura en espera.";
                LogError(ex);
                return Request.CreateResponse(HttpStatusCode.BadRequest, response);
            }
            catch (Exception ex)
            {
                response.Ok = false;
                response.Message = "Error interno al actualizar factura en espera.";
                LogError(ex);
                return Request.CreateResponse(HttpStatusCode.InternalServerError, response);
            }
        }

        [HttpGet]
        [Route("list-holds")]
        public HttpResponseMessage ListHolds(int storeId = 0, string search = "")
        {
            var connectionString = GetConnectionString();
            try
            {
                var holds = new List<NovaRetailOrderSummaryDto>();
                using (var cn = new SqlConnection(connectionString))
                {
                    cn.Open();
                    var sql = @"
                        SELECT TOP 50
                            th.ID AS OrderID, 1 AS Type, th.HoldComment AS Comment,
                            0 AS Total, 0 AS Tax, th.TransactionTime AS Time,
                            th.ExpirationOrDueDate, th.CustomerID, th.ReferenceNumber,
                            '' AS CashierName,
                            (SELECT COUNT(1) FROM dbo.TransactionHoldEntry the2
                             WHERE the2.TransactionHoldID = th.ID) AS ItemCount
                        FROM dbo.TransactionHold th
                        WHERE (@StoreID = 0 OR th.StoreID = @StoreID)
                          AND (@Search = '' OR th.HoldComment LIKE '%' + @Search + '%'
                               OR CAST(th.ID AS NVARCHAR(20)) LIKE '%' + @Search + '%'
                               OR th.ReferenceNumber LIKE '%' + @Search + '%')
                        ORDER BY th.TransactionTime DESC";

                    using (var cmd = new SqlCommand(sql, cn))
                    {
                        cmd.CommandTimeout = 30;
                        cmd.Parameters.AddWithValue("@StoreID", storeId);
                        cmd.Parameters.AddWithValue("@Search", search ?? string.Empty);
                        using (var reader = cmd.ExecuteReader())
                        {
                            while (reader.Read())
                            {
                                holds.Add(new NovaRetailOrderSummaryDto
                                {
                                    OrderID = Convert.ToInt32(reader["OrderID"]),
                                    Type = HoldRecallType,
                                    Comment = reader["Comment"] == DBNull.Value ? string.Empty : Convert.ToString(reader["Comment"]),
                                    Total = 0m,
                                    Tax = 0m,
                                    Time = Convert.ToDateTime(reader["Time"]),
                                    ExpirationOrDueDate = reader["ExpirationOrDueDate"] == DBNull.Value ? (DateTime?)null : Convert.ToDateTime(reader["ExpirationOrDueDate"]),
                                    CustomerID = reader["CustomerID"] == DBNull.Value ? 0 : Convert.ToInt32(reader["CustomerID"]),
                                    ItemCount = Convert.ToInt32(reader["ItemCount"]),
                                    ReferenceNumber = reader["ReferenceNumber"] == DBNull.Value ? string.Empty : Convert.ToString(reader["ReferenceNumber"]),
                                    CashierName = string.Empty
                                });
                            }
                        }
                    }
                }
                return Request.CreateResponse(HttpStatusCode.OK, new NovaRetailListOrdersResponse
                {
                    Ok = true,
                    Orders = holds
                });
            }
            catch (Exception ex)
            {
                LogError(ex);
                return Request.CreateResponse(HttpStatusCode.InternalServerError, new NovaRetailListOrdersResponse
                {
                    Ok = false,
                    Message = "Error interno al listar facturas en espera."
                });
            }
        }

        [HttpGet]
        [Route("hold-detail/{holdId}")]
        public HttpResponseMessage HoldDetail(int holdId)
        {
            var connectionString = GetConnectionString();
            try
            {
                NovaRetailOrderDetailDto hold = null;
                using (var cn = new SqlConnection(connectionString))
                {
                    cn.Open();
                    using (var cmd = new SqlCommand(
                        "SELECT ID, HoldComment, TransactionTime FROM dbo.TransactionHold WHERE ID = @HoldID", cn))
                    {
                        cmd.Parameters.AddWithValue("@HoldID", holdId);
                        using (var reader = cmd.ExecuteReader())
                        {
                            if (reader.Read())
                            {
                                hold = new NovaRetailOrderDetailDto
                                {
                                    OrderID = Convert.ToInt32(reader["ID"]),
                                    Type = HoldRecallType,
                                    Comment = reader["HoldComment"] == DBNull.Value ? string.Empty : Convert.ToString(reader["HoldComment"]),
                                    Total = 0m,
                                    Tax = 0m,
                                    Time = Convert.ToDateTime(reader["TransactionTime"])
                                };
                            }
                        }
                    }

                    if (hold == null)
                        return Request.CreateResponse(HttpStatusCode.NotFound, new NovaRetailOrderDetailResponse { Ok = false, Message = "Factura en espera no encontrada." });

                    using (var cmd = new SqlCommand(@"
                           SELECT the.ID AS EntryID, the.ItemID,
                               ISNULL(the.Description, '') AS Description,
                               the.Price, the.FullPrice, 0 AS Cost,
                               the.QuantityReserved AS QuantityOnOrder,
                               ISNULL(the.SalesRepID, 0) AS SalesRepID,
                               the.Taxable,
                               ISNULL(the.ItemTaxID, 0) AS TaxID,
                               ISNULL(i.ItemType, 0) AS ItemType
                        FROM dbo.TransactionHoldEntry the
                           LEFT JOIN dbo.Item i ON i.ID = the.ItemID
                        WHERE the.TransactionHoldID = @HoldID
                        ORDER BY the.ID", cn))
                    {
                        cmd.Parameters.AddWithValue("@HoldID", holdId);
                        using (var reader = cmd.ExecuteReader())
                        {
                            while (reader.Read())
                            {
                                hold.Entries.Add(new NovaRetailOrderEntryDto
                                {
                                    EntryID = Convert.ToInt32(reader["EntryID"]),
                                    ItemID = Convert.ToInt32(reader["ItemID"]),
                                    Description = Convert.ToString(reader["Description"]),
                                    Price = reader["Price"] == DBNull.Value ? 0m : Convert.ToDecimal(reader["Price"]),
                                    FullPrice = reader["FullPrice"] == DBNull.Value ? 0m : Convert.ToDecimal(reader["FullPrice"]),
                                    Cost = 0m,
                                    QuantityOnOrder = reader["QuantityOnOrder"] == DBNull.Value ? 1m : Convert.ToDecimal(reader["QuantityOnOrder"]),
                                    SalesRepID = reader["SalesRepID"] == DBNull.Value ? 0 : Convert.ToInt32(reader["SalesRepID"]),
                                    Taxable = reader["Taxable"] != DBNull.Value && Convert.ToInt32(reader["Taxable"]) != 0,
                                    TaxID = reader["TaxID"] == DBNull.Value ? 0 : Convert.ToInt32(reader["TaxID"]),
                                    ItemType = reader["ItemType"] == DBNull.Value ? 0 : Convert.ToInt32(reader["ItemType"])
                                });
                            }
                        }
                    }
                }
                return Request.CreateResponse(HttpStatusCode.OK, new NovaRetailOrderDetailResponse { Ok = true, Order = hold });
            }
            catch (Exception ex)
            {
                LogError(ex);
                return Request.CreateResponse(HttpStatusCode.InternalServerError, new NovaRetailOrderDetailResponse { Ok = false, Message = "Error interno al consultar detalle de factura en espera." });
            }
        }

        [HttpGet]
        [Route("list-orders")]
        public HttpResponseMessage ListOrders(int storeId = 0, int type = 3, string search = "")
        {
            var connectionString = GetConnectionString();
            try
            {
                var orders = new List<NovaRetailOrderSummaryDto>();
                using (var cn = new SqlConnection(connectionString))
                {
                    cn.Open();
                    var sql = @"
                        SELECT TOP 50
                            o.ID AS OrderID, o.[Type], o.Comment, o.Total, o.Tax,
                            o.[Time], o.ExpirationOrDueDate, o.CustomerID, o.ReferenceNumber,
                            ISNULL(c.Name, '') AS CashierName,
                            (SELECT COUNT(1) FROM dbo.OrderEntry oe WHERE oe.OrderID = o.ID) AS ItemCount
                        FROM dbo.[Order] o
                        LEFT JOIN dbo.Cashier c ON c.ID = o.SalesRepID
                        WHERE o.[Type] = @Type
                          AND o.Closed = 0
                          AND (@StoreID = 0 OR o.StoreID = @StoreID)
                          AND (@Search = '' OR o.Comment LIKE '%' + @Search + '%'
                               OR CAST(o.ID AS NVARCHAR(20)) LIKE '%' + @Search + '%'
                               OR o.ReferenceNumber LIKE '%' + @Search + '%'
                               OR c.Name LIKE '%' + @Search + '%')
                        ORDER BY o.[Time] DESC";

                    using (var cmd = new SqlCommand(sql, cn))
                    {
                        cmd.CommandTimeout = 30;
                        cmd.Parameters.AddWithValue("@Type", type);
                        cmd.Parameters.AddWithValue("@StoreID", storeId);
                        cmd.Parameters.AddWithValue("@Search", search ?? string.Empty);

                        using (var reader = cmd.ExecuteReader())
                        {
                            while (reader.Read())
                            {
                                orders.Add(new NovaRetailOrderSummaryDto
                                {
                                    OrderID = Convert.ToInt32(reader["OrderID"]),
                                    Type = Convert.ToInt32(reader["Type"]),
                                    Comment = reader["Comment"] == DBNull.Value ? string.Empty : Convert.ToString(reader["Comment"]),
                                    Total = reader["Total"] == DBNull.Value ? 0m : Convert.ToDecimal(reader["Total"]),
                                    Tax = reader["Tax"] == DBNull.Value ? 0m : Convert.ToDecimal(reader["Tax"]),
                                    Time = Convert.ToDateTime(reader["Time"]),
                                    ExpirationOrDueDate = reader["ExpirationOrDueDate"] == DBNull.Value ? (DateTime?)null : Convert.ToDateTime(reader["ExpirationOrDueDate"]),
                                    CustomerID = reader["CustomerID"] == DBNull.Value ? 0 : Convert.ToInt32(reader["CustomerID"]),
                                    ItemCount = Convert.ToInt32(reader["ItemCount"]),
                                    ReferenceNumber = reader["ReferenceNumber"] == DBNull.Value ? string.Empty : Convert.ToString(reader["ReferenceNumber"]),
                                    CashierName = reader["CashierName"] == DBNull.Value ? string.Empty : Convert.ToString(reader["CashierName"])
                                });
                            }
                        }
                    }
                }

                return Request.CreateResponse(HttpStatusCode.OK, new NovaRetailListOrdersResponse
                {
                    Ok = true,
                    Orders = orders
                });
            }
            catch (Exception ex)
            {
                LogError(ex);
                return Request.CreateResponse(HttpStatusCode.InternalServerError, new NovaRetailListOrdersResponse
                {
                    Ok = false,
                    Message = "Error interno al listar órdenes."
                });
            }
        }

        [HttpGet]
        [Route("order-detail/{orderId}")]
        public HttpResponseMessage OrderDetail(int orderId)
        {
            var connectionString = GetConnectionString();
            try
            {
                NovaRetailOrderDetailDto order = null;
                using (var cn = new SqlConnection(connectionString))
                {
                    cn.Open();

                    using (var cmd = new SqlCommand(@"
                        SELECT ID, [Type], Comment, Total, Tax, [Time]
                        FROM dbo.[Order]
                        WHERE ID = @OrderID", cn))
                    {
                        cmd.Parameters.AddWithValue("@OrderID", orderId);
                        using (var reader = cmd.ExecuteReader())
                        {
                            if (reader.Read())
                            {
                                order = new NovaRetailOrderDetailDto
                                {
                                    OrderID = Convert.ToInt32(reader["ID"]),
                                    Type = Convert.ToInt32(reader["Type"]),
                                    Comment = reader["Comment"] == DBNull.Value ? string.Empty : Convert.ToString(reader["Comment"]),
                                    Total = reader["Total"] == DBNull.Value ? 0m : Convert.ToDecimal(reader["Total"]),
                                    Tax = reader["Tax"] == DBNull.Value ? 0m : Convert.ToDecimal(reader["Tax"]),
                                    Time = Convert.ToDateTime(reader["Time"])
                                };
                            }
                        }
                    }

                    if (order == null)
                    {
                        return Request.CreateResponse(HttpStatusCode.NotFound, new NovaRetailOrderDetailResponse
                        {
                            Ok = false,
                            Message = "Orden no encontrada."
                        });
                    }

                    using (var cmd = new SqlCommand(@"
                        SELECT oe.ID AS EntryID, oe.ItemID, oe.Description, oe.Price, oe.FullPrice,
                               oe.Cost, oe.QuantityOnOrder, ISNULL(oe.SalesRepID, 0) AS SalesRepID, oe.Taxable,
                               ISNULL(i.TaxID, 0) AS TaxID,
                               ISNULL(i.ItemType, 0) AS ItemType,
                               ISNULL(oe.PriceSource, 1) AS PriceSource,
                               ISNULL(oe.DetailID, 0) AS DetailID,
                               ISNULL(oe.Comment, '') AS Comment,
                               ISNULL(oe.DiscountReasonCodeID, 0) AS DiscountReasonCodeID,
                               ISNULL(oe.ReturnReasonCodeID, 0) AS ReturnReasonCodeID,
                               ISNULL(oe.TaxChangeReasonCodeID, 0) AS TaxChangeReasonCodeID
                        FROM dbo.OrderEntry oe
                        LEFT JOIN dbo.Item i ON i.ID = oe.ItemID
                        WHERE oe.OrderID = @OrderID
                        ORDER BY oe.ID", cn))
                    {
                        cmd.Parameters.AddWithValue("@OrderID", orderId);
                        using (var reader = cmd.ExecuteReader())
                        {
                            while (reader.Read())
                            {
                                order.Entries.Add(new NovaRetailOrderEntryDto
                                {
                                    EntryID = Convert.ToInt32(reader["EntryID"]),
                                    ItemID = Convert.ToInt32(reader["ItemID"]),
                                    Description = reader["Description"] == DBNull.Value ? string.Empty : Convert.ToString(reader["Description"]),
                                    Price = reader["Price"] == DBNull.Value ? 0m : Convert.ToDecimal(reader["Price"]),
                                    FullPrice = reader["FullPrice"] == DBNull.Value ? 0m : Convert.ToDecimal(reader["FullPrice"]),
                                    Cost = reader["Cost"] == DBNull.Value ? 0m : Convert.ToDecimal(reader["Cost"]),
                                    QuantityOnOrder = reader["QuantityOnOrder"] == DBNull.Value ? 0m : Convert.ToDecimal(reader["QuantityOnOrder"]),
                                    SalesRepID = reader["SalesRepID"] == DBNull.Value ? 0 : Convert.ToInt32(reader["SalesRepID"]),
                                    Taxable = reader["Taxable"] != DBNull.Value && Convert.ToInt32(reader["Taxable"]) != 0,
                                    TaxID = reader["TaxID"] == DBNull.Value ? 0 : Convert.ToInt32(reader["TaxID"]),
                                    ItemType = reader["ItemType"] == DBNull.Value ? 0 : Convert.ToInt32(reader["ItemType"]),
                                    PriceSource = reader["PriceSource"] == DBNull.Value ? 1 : Convert.ToInt32(reader["PriceSource"]),
                                    DetailID = reader["DetailID"] == DBNull.Value ? 0 : Convert.ToInt32(reader["DetailID"]),
                                    Comment = reader["Comment"] == DBNull.Value ? string.Empty : Convert.ToString(reader["Comment"]),
                                    DiscountReasonCodeID = reader["DiscountReasonCodeID"] == DBNull.Value ? 0 : Convert.ToInt32(reader["DiscountReasonCodeID"]),
                                    ReturnReasonCodeID = reader["ReturnReasonCodeID"] == DBNull.Value ? 0 : Convert.ToInt32(reader["ReturnReasonCodeID"]),
                                    TaxChangeReasonCodeID = reader["TaxChangeReasonCodeID"] == DBNull.Value ? 0 : Convert.ToInt32(reader["TaxChangeReasonCodeID"])
                                });
                            }
                        }
                    }
                }

                return Request.CreateResponse(HttpStatusCode.OK, new NovaRetailOrderDetailResponse
                {
                    Ok = true,
                    Order = order
                });
            }
            catch (Exception ex)
            {
                LogError(ex);
                return Request.CreateResponse(HttpStatusCode.InternalServerError, new NovaRetailOrderDetailResponse
                {
                    Ok = false,
                    Message = "Error interno al consultar detalle de orden."
                });
            }
        }

        [HttpDelete]
        [Route("delete-work-order/{orderId}")]
        public HttpResponseMessage DeleteWorkOrder(int orderId)
        {
            if (orderId <= 0)
            {
                return Request.CreateResponse(HttpStatusCode.BadRequest, new NovaRetailCreateQuoteResponse
                {
                    Ok = false,
                    Message = "Se requiere un OrderID válido."
                });
            }

            var response = new NovaRetailCreateQuoteResponse();
            var connectionString = GetConnectionString();

            try
            {
                using (var cn = new SqlConnection(connectionString))
                {
                    cn.Open();

                    using (var tx = cn.BeginTransaction())
                    {
                        try
                        {
                            var rowsAffected = CloseWorkOrder(cn, orderId, tx);
                            if (rowsAffected == 0)
                            {
                                tx.Rollback();
                                response.Ok = false;
                                response.Message = $"No se encontró la orden de trabajo #{orderId} o ya estaba cerrada.";
                                return Request.CreateResponse(HttpStatusCode.BadRequest, response);
                            }

                            tx.Commit();
                        }
                        catch
                        {
                            tx.Rollback();
                            throw;
                        }
                    }
                }

                response.Ok = true;
                response.OrderID = orderId;
                response.Message = "Orden de trabajo cancelada y stock liberado.";
                return Request.CreateResponse(HttpStatusCode.OK, response);
            }
            catch (SqlException ex)
            {
                response.Ok = false;
                response.Message = "Error de base de datos al cancelar orden de trabajo.";
                LogError(ex);
                return Request.CreateResponse(HttpStatusCode.BadRequest, response);
            }
            catch (Exception ex)
            {
                response.Ok = false;
                response.Message = "Error interno al cancelar orden de trabajo.";
                LogError(ex);
                return Request.CreateResponse(HttpStatusCode.InternalServerError, response);
            }
        }

        [HttpDelete]
        [Route("delete-quote/{orderId}")]
        public HttpResponseMessage DeleteQuote(int orderId)
        {
            if (orderId <= 0)
            {
                return Request.CreateResponse(HttpStatusCode.BadRequest, new NovaRetailCreateQuoteResponse
                {
                    Ok = false,
                    Message = "Se requiere un OrderID válido."
                });
            }

            var response = new NovaRetailCreateQuoteResponse();
            var connectionString = GetConnectionString();

            try
            {
                using (var cn = new SqlConnection(connectionString))
                {
                    cn.Open();
                    var rowsAffected = CloseQuoteOrder(cn, orderId);
                    if (rowsAffected == 0)
                    {
                        response.Ok = false;
                        response.Message = $"No se encontró la cotización #{orderId} o ya estaba cerrada.";
                        return Request.CreateResponse(HttpStatusCode.BadRequest, response);
                    }
                }

                response.Ok = true;
                response.OrderID = orderId;
                response.Message = "Cotización cancelada y marcada como cerrada.";
                return Request.CreateResponse(HttpStatusCode.OK, response);
            }
            catch (SqlException ex)
            {
                response.Ok = false;
                response.Message = "Error de base de datos al cancelar cotización.";
                LogError(ex);
                return Request.CreateResponse(HttpStatusCode.BadRequest, response);
            }
            catch (Exception ex)
            {
                response.Ok = false;
                response.Message = "Error interno al cancelar cotización.";
                LogError(ex);
                return Request.CreateResponse(HttpStatusCode.InternalServerError, response);
            }
        }

        [HttpDelete]
        [Route("delete-hold/{holdId}")]
        public HttpResponseMessage DeleteHold(int holdId)
        {
            if (holdId <= 0)
            {
                return Request.CreateResponse(HttpStatusCode.BadRequest, new NovaRetailCreateQuoteResponse
                {
                    Ok = false,
                    Message = "Se requiere un HoldID válido."
                });
            }

            var response = new NovaRetailCreateQuoteResponse();
            var connectionString = GetConnectionString();

            try
            {
                using (var cn = new SqlConnection(connectionString))
                {
                    cn.Open();
                    DeleteTransactionHold(cn, holdId);
                }

                response.Ok = true;
                response.OrderID = holdId;
                response.Message = "Factura en espera eliminada.";
                return Request.CreateResponse(HttpStatusCode.OK, response);
            }
            catch (SqlException ex)
            {
                response.Ok = false;
                response.Message = "Error de base de datos al eliminar factura en espera.";
                LogError(ex);
                return Request.CreateResponse(HttpStatusCode.BadRequest, response);
            }
            catch (Exception ex)
            {
                response.Ok = false;
                response.Message = "Error interno al eliminar factura en espera.";
                LogError(ex);
                return Request.CreateResponse(HttpStatusCode.InternalServerError, response);
            }
        }
        }
    }
