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

            foreach (var item in request.Items)
            {
                if (item == null)
                    continue;

                var fullPrice = item.DisplayFullPrice ?? item.FullPrice ?? item.UnitPrice;
                var price = item.DisplayPrice ?? item.UnitPrice;
                if (fullPrice > price || item.DiscountReasonCodeID > 0)
                {
                    NovaRetailAuditLogger.Log(
                        cn,
                        "DiscountApplied",
                        "Sale",
                        transactionNumber,
                        request.CashierID,
                        request.StoreID,
                        request.RegisterID,
                        Math.Max(0m, fullPrice - price),
                        $"Item {item.ItemID}: descuento/precio menor de {fullPrice:N2} a {price:N2}. Motivo {item.DiscountReasonCodeID}");
                }
                else if (item.PriceSource != 1 || price > fullPrice)
                {
                    NovaRetailAuditLogger.Log(
                        cn,
                        "PriceChanged",
                        "Sale",
                        transactionNumber,
                        request.CashierID,
                        request.StoreID,
                        request.RegisterID,
                        price,
                        $"Item {item.ItemID}: precio cambiado de {fullPrice:N2} a {price:N2}. PriceSource {item.PriceSource}");
                }
            }
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
                    Message = "Solicitud invÃƒÆ’Ã‚Â¡lida."
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

                        var itemsParameter = cmd.Parameters.AddWithValue("@Items", NovaRetailSalesSqlMapper.ToItemsTable(request.Items));
                        itemsParameter.SqlDbType = SqlDbType.Structured;
                        itemsParameter.TypeName = "dbo.NovaRetailSaleItemTVP";

                        var tendersParameter = cmd.Parameters.AddWithValue("@Tenders", NovaRetailSalesSqlMapper.ToTendersTable(request.Tenders));
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
                            SyncPersistedTransactionEntryValues(cn, response.TransactionNumber, request);
                        }
                        catch (Exception exEntries)
                        {
                            response.Warnings.Add($"TransactionEntry: {exEntries.Message}");
                        }

                        try
                        {
                            var correctedTotals = SyncPersistedTransactionTotals(cn, response.TransactionNumber, request);
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

                        LogSaleItemAudit(cn, request, response.TransactionNumber);

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
