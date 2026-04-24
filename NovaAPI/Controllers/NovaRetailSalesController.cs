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

        private static string GetConnectionString()
        {
            var connectionString = AppConfig.ConnectionString("RMHPOS");
            if (string.IsNullOrWhiteSpace(connectionString))
                throw new ConfigurationErrorsException("No se encontrÃƒÂ³ la cadena de conexiÃƒÂ³n RMHPOS para registrar ventas.");

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
                    Message = "Solicitud invÃƒÂ¡lida."
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
                        ? "Solicitud invÃƒÂ¡lida."
                        : string.Join(" | ", errors)
                });
            }

            if (request.Items == null || request.Items.Count == 0)
            {
                return Request.CreateResponse(HttpStatusCode.BadRequest, new NovaRetailCreateSaleResponse
                {
                    Ok = false,
                    Message = "La venta no contiene ÃƒÂ­tems."
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
                                    var orderLabel = orderType == WorkOrderType ? "Orden de trabajo" : "CotizaciÃƒÂ³n";
                                    response.Warnings.Add($"CloseQuote: {orderLabel} #{request.RecallID} no encontrada o ya cerrada.");
                                }
                            }
                            catch (Exception exQuote)
                            {
                                response.Warnings.Add($"CloseQuote: {exQuote.Message}");
                            }
                        }

                        // Ã¢â€â‚¬Ã¢â€â‚¬ AR Transaction: create entry for credit sales / credit NCs Ã¢â€â‚¬Ã¢â€â‚¬
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

            // Leer cÃƒÂ©dula del emisor Ã¢â‚¬â€ intentar varias tablas posibles
            var cedulaEmisor = string.Empty;
            var cedulaQueries = new[]
            {
                // VATDetailID almacena la cÃƒÂ©dula jurÃƒÂ­dica/fÃƒÂ­sica del emisor en RMH Costa Rica
                "SELECT TOP 1 ISNULL(CAST(VATDetailID AS NVARCHAR(20)),'')           FROM dbo.[Configuration]",
                // VATRegistrationNumber puede contener un cÃƒÂ³digo interno (ej. "201"), no la cÃƒÂ©dula real
                "SELECT TOP 1 ISNULL(CAST(VATRegistrationNumber AS NVARCHAR(20)),'') FROM dbo.[Configuration]",
                // Fallbacks por si la instalaciÃƒÂ³n usa otra tabla/columna
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
                            // Normalizar: quitar guiones y espacios (ej. "3-101-639680" Ã¢â€ â€™ "3101639680")
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
            // Usar COMPROBANTE_INTERNO (consecutivo de AVS_INTEGRAFAST_02) si estÃƒÂ¡ disponible
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

            // Cargar TransactionEntry: indexar por DetailID y por posiciÃƒÂ³n secuencial
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

                // 1) Match por DetailID = RowNo - 1 (asignaciÃƒÂ³n estÃƒÂ¡ndar del SP)
                var detailId = item.RowNo - 1;
                int transactionEntryId = 0;

                if (entriesByDetailId.TryGetValue(detailId, out var byDetailId) && byDetailId > 0)
                {
                    transactionEntryId = byDetailId;
                }
                else
                {
                    // 2) Fallback: posiciÃƒÂ³n secuencial (ÃƒÂ­ndice RowNo-1 en la lista)
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
            var cedulaColumn = ColumnExists(cn, "AVS_INTEGRAFAST_02", "CEDULA") ? "CEDULA" : "PROVEEDOR_SISTEMA";

            if (consecutivoCol != null)
            {
                try
                {
                    using (var incCmd = new SqlCommand(
                        $"UPDATE dbo.AVS_INTEGRAFAST_02 SET {consecutivoCol} = {consecutivoCol} + 1 " +
                        $"OUTPUT INSERTED.{consecutivoCol} AS Consecutivo, INSERTED.COD_SUCURSAL, INSERTED.{cedulaColumn} AS CedulaTributaria", cn))
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
                                var ced = rd["CedulaTributaria"];
                                if (ced != DBNull.Value && !string.IsNullOrWhiteSpace(Convert.ToString(ced)))
                                    request.CedulaTributaria = Convert.ToString(ced);
                            }
                        }
                    }
                }
                catch { /* tabla AVS_INTEGRAFAST_02 no existe Ã¢â‚¬â€ usar valores del request */ }
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

            var medioPagos = ResolveIntegraFastMedioPagos(cn, request.Tenders);

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
                // SP no existe Ã¢â‚¬â€ INSERT directo
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

        private static bool ColumnExists(SqlConnection cn, string tableName, string columnName)
        {
            using (var cmd = new SqlCommand(
                @"SELECT COUNT(1)
                  FROM INFORMATION_SCHEMA.COLUMNS
                  WHERE TABLE_NAME = @TableName
                    AND COLUMN_NAME = @ColumnName", cn))
            {
                cmd.Parameters.AddWithValue("@TableName", tableName);
                cmd.Parameters.AddWithValue("@ColumnName", columnName);
                return Convert.ToInt32(cmd.ExecuteScalar()) > 0;
            }
        }

        private sealed class TenderFiscalInfo
        {
            public string Code { get; set; } = string.Empty;
            public string Description { get; set; } = string.Empty;
        }

        private static List<string> ResolveIntegraFastMedioPagos(SqlConnection cn, IEnumerable<NovaRetailSaleTenderDto> tenders)
        {
            var orderedTenders = (tenders ?? Enumerable.Empty<NovaRetailSaleTenderDto>())
                .OrderBy(t => t.RowNo)
                .Take(4)
                .ToList();

            var tenderInfo = LoadTenderFiscalInfo(cn, orderedTenders.Select(t => t.TenderID));
            return orderedTenders
                .Select(t =>
                {
                    TenderFiscalInfo info;
                    tenderInfo.TryGetValue(t.TenderID, out info);
                    return ResolveIntegraFastMedioPagoCodigo(t, info);
                })
                .ToList();
        }

        private static Dictionary<int, TenderFiscalInfo> LoadTenderFiscalInfo(SqlConnection cn, IEnumerable<int> tenderIds)
        {
            var ids = (tenderIds ?? Enumerable.Empty<int>())
                .Where(id => id > 0)
                .Distinct()
                .ToList();

            var result = new Dictionary<int, TenderFiscalInfo>();
            if (ids.Count == 0)
                return result;

            var parameterNames = ids.Select((id, index) => $"@TenderID{index}").ToList();
            var sql = $"SELECT ID, ISNULL(Code, '') AS Code, ISNULL(Description, '') AS Description FROM dbo.Tender WHERE ID IN ({string.Join(", ", parameterNames)})";

            using (var cmd = new SqlCommand(sql, cn))
            {
                for (var index = 0; index < ids.Count; index++)
                    cmd.Parameters.AddWithValue(parameterNames[index], ids[index]);

                using (var reader = cmd.ExecuteReader())
                {
                    while (reader.Read())
                    {
                        result[Convert.ToInt32(reader["ID"])] = new TenderFiscalInfo
                        {
                            Code = reader["Code"] == DBNull.Value ? string.Empty : Convert.ToString(reader["Code"]),
                            Description = reader["Description"] == DBNull.Value ? string.Empty : Convert.ToString(reader["Description"])
                        };
                    }
                }
            }

            return result;
        }

        private static string ResolveIntegraFastMedioPagoCodigo(NovaRetailSaleTenderDto tender, TenderFiscalInfo info)
        {
            var tenderCode = info != null ? ExtractIntegraFastMedioPagoFromTenderCode(info.Code) : string.Empty;
            if (!string.IsNullOrWhiteSpace(tenderCode))
                return tenderCode;

            if (!string.IsNullOrWhiteSpace(tender?.MedioPagoCodigo))
                return tender.MedioPagoCodigo.Trim();

            var description = info != null && !string.IsNullOrWhiteSpace(info.Description)
                ? info.Description
                : tender?.Description;

            return InferIntegraFastMedioPagoCodigo(description);
        }

        private static string ExtractIntegraFastMedioPagoFromTenderCode(string tenderCode)
        {
            var value = (tenderCode ?? string.Empty).Trim();
            if (value.Length >= 2 && char.IsDigit(value[0]) && char.IsDigit(value[1]))
                return value.Substring(0, 2);

            return string.Empty;
        }

        private static string InferIntegraFastMedioPagoCodigo(string description)
        {
            var value = (description ?? string.Empty).Trim().ToUpperInvariant();
            if (value.Contains("EFECTIVO") || value.Contains("CONTADO"))
                return "01";
            if (value.Contains("TARJETA"))
                return "02";
            if (value.Contains("TRANSFER") || value.Contains("SINPE"))
                return "04";
            if (value.Contains("CRÃƒâ€°DITO") || value.Contains("CREDITO"))
                return "99";

            return string.Empty;
        }

        /// <summary>
        /// Inserta las lÃƒÂ­neas de detalle en AVS_INTEGRAFAST_05 para que fxAVS_GetLineaDetalle
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

            // Cargar info de artÃƒÂ­culos (Cabys, Code) desde Item table
            var itemInfoMap = new Dictionary<int, (string Cabys, string Code, string Description)>();
            var itemIds = request.Items.Select(i => i.ItemID).Distinct().ToList();
            if (itemIds.Count > 0)
            {
                var parameters = itemIds.Select((id, i) => $"@id{i}").ToList();
                var idList = string.Join(",", parameters);
                try
                {
                    using (var cmd = new SqlCommand(
                        $"SELECT ID, ISNULL(CAST(SubDescription3 AS NVARCHAR(20)),'') AS Cabys, ISNULL(ItemLookupCode,'') AS Code, ISNULL(Description,'') AS Desc1 FROM dbo.Item WHERE ID IN ({idList})", cn))
                    {
                        for (int pi = 0; pi < itemIds.Count; pi++)
                            cmd.Parameters.AddWithValue($"@id{pi}", itemIds[pi]);

                        using (var r = cmd.ExecuteReader())
                        {
                            while (r.Read())
                            {
                                var id = Convert.ToInt32(r["ID"]);
                                var cabys = NormalizeCabys(r["Cabys"]?.ToString());
                                var code = r["Code"]?.ToString() ?? string.Empty;
                                var desc = r["Desc1"]?.ToString() ?? string.Empty;
                                itemInfoMap[id] = (cabys, code, desc);
                            }
                        }
                    }
                }
                catch { /* tabla Item podrÃƒÂ­a tener diferente esquema */ }
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
                var montoImpuesto = Math.Round(item.SalesTax, 2);
                // Mantener la metadata base del impuesto original de la lÃƒÂ­nea, aunque
                // la exoneraciÃƒÂ³n reduzca el impuesto efectivo a cobrar.
                var baseTaxAmount = Math.Round(item.SalesTax + Math.Max(0m, item.ExMonto), 2);
                var baseTaxRate = baseTaxAmount > 0 && subTotal > 0
                    ? Math.Round(baseTaxAmount / subTotal * 100m, 2)
                    : 0m;
                var montoLinea = subTotal + montoImpuesto;
                var hasExoneration = !string.IsNullOrWhiteSpace(item.ExNumeroDoc);

                itemInfoMap.TryGetValue(item.ItemID, out var info);
                var cabys = info.Cabys ?? string.Empty;
                var codProducto = info.Code ?? item.ItemID.ToString();
                var detalle = !string.IsNullOrWhiteSpace(item.ExtendedDescription)
                    ? item.ExtendedDescription
                    : !string.IsNullOrWhiteSpace(info.Description) ? info.Description
                    : item.ItemID.ToString();

                // CÃƒÂ³digo de tarifa IVA segÃƒÂºn normativa CR
                var codTarifaIVA = baseTaxRate > 0 ? ResolveIntegraFastTaxCode(baseTaxRate) : string.Empty;

                var naturalezaDescuento = montoDescuento > 0 ? (item.LineComment ?? "Descuento comercial") : string.Empty;

                // Calcular porcentaje exoneraciÃƒÂ³n para EXONERA_PORCENTAJE_COMPRA
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
                        cmd.Parameters.AddWithValue("@COD_IMPUESTO", codTarifaIVA);
                        cmd.Parameters.AddWithValue("@COD_IMPUESTO_BASE", baseTaxRate > 0 ? (object)codTarifaIVA : DBNull.Value);
                        cmd.Parameters.AddWithValue("@TARIFA_IMPUESTO", baseTaxRate > 0 ? baseTaxRate : 0m);
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

        private static string ResolveIntegraFastTaxCode(decimal taxRate)
        {
            if (taxRate >= 13m) return "08";
            if (taxRate >= 4m) return "04";
            if (taxRate >= 2m) return "07";
            if (taxRate >= 1m) return "06";
            return "01";
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

        private static string NormalizeCabys(string value)
        {
            if (string.IsNullOrWhiteSpace(value))
                return string.Empty;

            return new string(value.Where(char.IsDigit).ToArray());
        }

        private sealed class LedgerBalanceSnapshot
        {
            public decimal BaseAmount { get; set; }
            public decimal AppliedToEntry { get; set; }
            public decimal AppliedByEntry { get; set; }
        }

        private sealed class SaleTotalsSnapshot
        {
            public decimal SubTotal { get; set; }
            public decimal Discounts { get; set; }
            public decimal SalesTax { get; set; }
            public decimal Total { get; set; }
        }

        private static string NormalizeTransactionReference(string reference)
        {
            var value = (reference ?? string.Empty).Trim();
            if (value.StartsWith("TR:", StringComparison.OrdinalIgnoreCase))
                value = value.Substring(3).Trim();

            return value;
        }

        private static string BuildLedgerReference(string transactionReference)
        {
            var normalized = NormalizeTransactionReference(transactionReference);
            return string.IsNullOrWhiteSpace(normalized)
                ? string.Empty
                : "TR:" + normalized;
        }

        private static SaleTotalsSnapshot CalculateRequestedSaleTotals(NovaRetailCreateSaleRequest request)
        {
            var totals = new SaleTotalsSnapshot();
            if (request?.Items == null || request.Items.Count == 0)
                return totals;

            foreach (var item in request.Items)
            {
                var qty = item.Quantity <= 0 ? 1m : item.Quantity;
                var unitPrice = item.UnitPrice;
                var fullPrice = item.FullPrice ?? unitPrice;
                var grossAmount = Math.Round(fullPrice * qty, 2, MidpointRounding.AwayFromZero);
                var discountAmount = Math.Round(item.LineDiscountAmount, 2, MidpointRounding.AwayFromZero);

                if (discountAmount == 0m && fullPrice > unitPrice)
                    discountAmount = Math.Round((fullPrice - unitPrice) * qty, 2, MidpointRounding.AwayFromZero);

                var subTotal = Math.Round(grossAmount - discountAmount, 2, MidpointRounding.AwayFromZero);
                var salesTax = Math.Round(item.SalesTax, 2, MidpointRounding.AwayFromZero);

                totals.SubTotal += subTotal;
                totals.Discounts += discountAmount;
                totals.SalesTax += salesTax;
                totals.Total += subTotal + salesTax;
            }

            totals.SubTotal = Math.Round(totals.SubTotal, 2, MidpointRounding.AwayFromZero);
            totals.Discounts = Math.Round(totals.Discounts, 2, MidpointRounding.AwayFromZero);
            totals.SalesTax = Math.Round(totals.SalesTax, 2, MidpointRounding.AwayFromZero);
            totals.Total = Math.Round(totals.Total, 2, MidpointRounding.AwayFromZero);

            return totals;
        }

        private static SaleTotalsSnapshot SyncPersistedTransactionTotals(SqlConnection cn, int transactionNumber, NovaRetailCreateSaleRequest request)
        {
            var totals = CalculateRequestedSaleTotals(request);
            if (transactionNumber <= 0 || totals.Total <= 0m)
                return totals;

            using (var cmd = new SqlCommand(@"
UPDATE dbo.[Transaction]
SET Total = @Total,
    SalesTax = @SalesTax
WHERE TransactionNumber = @TransactionNumber
  AND
  (
      ABS(ISNULL(Total, 0) - @Total) > @Tolerance
      OR ABS(ISNULL(SalesTax, 0) - @SalesTax) > @Tolerance
  );", cn))
            {
                cmd.Parameters.AddWithValue("@TransactionNumber", transactionNumber);
                cmd.Parameters.AddWithValue("@Total", totals.Total);
                cmd.Parameters.AddWithValue("@SalesTax", totals.SalesTax);
                cmd.Parameters.AddWithValue("@Tolerance", LedgerClosingTolerance);
                cmd.ExecuteNonQuery();
            }

            return totals;
        }

        private static decimal LoadPersistedTransactionTotal(SqlConnection cn, SqlTransaction tx, int transactionNumber, decimal fallback)
        {
            if (transactionNumber <= 0)
                return fallback;

            using (var cmd = new SqlCommand("SELECT TOP 1 Total FROM dbo.[Transaction] WHERE TransactionNumber = @TransactionNumber", cn))
            {
                if (tx != null)
                    cmd.Transaction = tx;

                cmd.Parameters.AddWithValue("@TransactionNumber", transactionNumber);
                var result = cmd.ExecuteScalar();
                if (result == null || result == DBNull.Value)
                    return fallback;

                return Math.Abs(Convert.ToDecimal(result));
            }
        }

        private static int ResolveReferencedLedgerEntryID(SqlConnection cn, SqlTransaction tx, int accountID, string transactionReference)
        {
            var ledgerReference = BuildLedgerReference(transactionReference);
            if (accountID <= 0 || string.IsNullOrWhiteSpace(ledgerReference))
                return 0;

            using (var cmd = new SqlCommand(@"
SELECT TOP 1 le.ID
FROM dbo.AR_LedgerEntry le
WHERE le.AccountID = @AccountID
  AND le.Reference = @Reference
  AND le.DocumentType IN (1, 2, 3, 4)
  AND EXISTS
  (
      SELECT 1
      FROM dbo.AR_LedgerEntryDetail d
      WHERE d.LedgerEntryID = le.ID
        AND d.AppliedEntryID = 0
        AND d.Amount > 0
  )
ORDER BY CASE WHEN le.[Open] = 1 THEN 0 ELSE 1 END,
         le.ID DESC;", cn))
            {
                if (tx != null)
                    cmd.Transaction = tx;

                cmd.Parameters.AddWithValue("@AccountID", accountID);
                cmd.Parameters.AddWithValue("@Reference", ledgerReference);

                var result = cmd.ExecuteScalar();
                return result == null || result == DBNull.Value
                    ? 0
                    : Convert.ToInt32(result);
            }
        }

        private static LedgerBalanceSnapshot LoadLedgerBalanceSnapshot(SqlConnection cn, SqlTransaction tx, int ledgerEntryID)
        {
            using (var cmd = new SqlCommand(@"
SELECT
    BaseAmount = ISNULL(
        (
            SELECT SUM(d.Amount)
            FROM dbo.AR_LedgerEntryDetail d
            WHERE d.LedgerEntryID = @LedgerEntryID
              AND d.AppliedEntryID = 0
        ), 0),
    AppliedToEntry = ISNULL(
        (
            SELECT SUM(d.AppliedAmount)
            FROM dbo.AR_LedgerEntryDetail d
            WHERE d.AppliedEntryID = @LedgerEntryID
        ), 0),
    AppliedByEntry = ISNULL(
        (
            SELECT SUM(ABS(d.AppliedAmount))
            FROM dbo.AR_LedgerEntryDetail d
            WHERE d.LedgerEntryID = @LedgerEntryID
              AND d.AppliedEntryID > 0
        ), 0);", cn))
            {
                if (tx != null)
                    cmd.Transaction = tx;

                cmd.Parameters.AddWithValue("@LedgerEntryID", ledgerEntryID);

                using (var reader = cmd.ExecuteReader())
                {
                    if (!reader.Read())
                        return new LedgerBalanceSnapshot();

                    return new LedgerBalanceSnapshot
                    {
                        BaseAmount = reader["BaseAmount"] == DBNull.Value ? 0m : Convert.ToDecimal(reader["BaseAmount"]),
                        AppliedToEntry = reader["AppliedToEntry"] == DBNull.Value ? 0m : Convert.ToDecimal(reader["AppliedToEntry"]),
                        AppliedByEntry = reader["AppliedByEntry"] == DBNull.Value ? 0m : Convert.ToDecimal(reader["AppliedByEntry"])
                    };
                }
            }
        }

        private static void SetLedgerEntryOpenState(SqlConnection cn, SqlTransaction tx, int ledgerEntryID, bool isOpen, DateTime now)
        {
            using (var cmd = new SqlCommand(@"
UPDATE dbo.AR_LedgerEntry
SET ClosingDate = CASE WHEN @Open = 1 THEN NULL ELSE ISNULL(ClosingDate, @Now) END,
    LastUpdated = @Now
WHERE ID = @LedgerEntryID;", cn))
            {
                if (tx != null)
                    cmd.Transaction = tx;

                cmd.Parameters.AddWithValue("@LedgerEntryID", ledgerEntryID);
                cmd.Parameters.AddWithValue("@Open", isOpen ? 1 : 0);
                cmd.Parameters.AddWithValue("@Now", now);
                cmd.ExecuteNonQuery();
            }
        }

        private static void RefreshLedgerApplicationStatuses(SqlConnection cn, SqlTransaction tx, int sourceLedgerEntryID, int targetLedgerEntryID, DateTime now)
        {
            if (sourceLedgerEntryID > 0)
            {
                var sourceSnapshot = LoadLedgerBalanceSnapshot(cn, tx, sourceLedgerEntryID);
                var sourceRemaining = Math.Max(0m, Math.Abs(sourceSnapshot.BaseAmount) - sourceSnapshot.AppliedByEntry);
                SetLedgerEntryOpenState(cn, tx, sourceLedgerEntryID, sourceRemaining > LedgerClosingTolerance, now);
            }

            if (targetLedgerEntryID > 0)
            {
                var targetSnapshot = LoadLedgerBalanceSnapshot(cn, tx, targetLedgerEntryID);
                var targetBalance = targetSnapshot.BaseAmount + targetSnapshot.AppliedToEntry;
                SetLedgerEntryOpenState(cn, tx, targetLedgerEntryID, targetBalance > LedgerClosingTolerance, now);
            }
        }

        private static void InsertLedgerApplicationDetail(
            SqlConnection cn,
            SqlTransaction tx,
            int ledgerEntryID,
            int accountID,
            int ledgerType,
            DateTime postingDate,
            string reference,
            int appliedEntryID,
            decimal appliedAmount)
        {
            using (var cmd = new SqlCommand("dbo.OFF_AR_LEDGERENTRYDETAIL_INSERT", cn, tx))
            {
                cmd.CommandType = CommandType.StoredProcedure;
                cmd.CommandTimeout = 60;

                cmd.Parameters.AddWithValue("@LedgerEntryID", ledgerEntryID);
                cmd.Parameters.AddWithValue("@AccountID", accountID);
                cmd.Parameters.AddWithValue("@LedgerType", ledgerType);
                cmd.Parameters.AddWithValue("@DueDate", postingDate);
                cmd.Parameters.AddWithValue("@PostingDate", postingDate);
                cmd.Parameters.AddWithValue("@DetailType", (byte)0);
                cmd.Parameters.AddWithValue("@Reference", reference);
                cmd.Parameters.AddWithValue("@Amount", 0m);
                cmd.Parameters.AddWithValue("@AmountLCY", 0m);
                cmd.Parameters.AddWithValue("@AmountACY", 0m);
                cmd.Parameters.AddWithValue("@AuditEntryID", 0);
                cmd.Parameters.AddWithValue("@AppliedEntryID", appliedEntryID);
                cmd.Parameters.AddWithValue("@AppliedAmount", appliedAmount);
                cmd.Parameters.AddWithValue("@UnapplyEntryID", 0);
                cmd.Parameters.AddWithValue("@UnapplyReasonID", 0);
                cmd.Parameters.AddWithValue("@ISCLOSING", false);

                cmd.ExecuteNonQuery();
            }
        }

        /// <summary>
        /// Creates an AR_Transaction entry when the sale is a credit sale or a credit-mode credit note.
        /// Credit sale (CondicionVenta=02, no NC): DocumentType=3 + LedgerType=3.
        /// Credit NC (has NC_REFERENCIA, MedioPago 99): DocumentType=3 + LedgerType=4.
        /// </summary>
        private static void TryCreateARTransaction(NovaRetailCreateSaleRequest request, NovaRetailCreateSaleResponse response)
        {
            if (response.TransactionNumber <= 0 ||
                (string.IsNullOrWhiteSpace(request.CodCliente) &&
                 string.IsNullOrWhiteSpace(request.CreditAccountNumber) &&
                 request.CustomerID <= 0))
                return;

            var total = Math.Abs(response.Total ?? 0m);
            if (total <= 0m)
                return;

            bool isNC = !string.IsNullOrWhiteSpace(request.NC_REFERENCIA);
            bool isCreditSale = string.Equals(request.CondicionVenta, "02", StringComparison.OrdinalIgnoreCase) && !isNC;
            bool isCreditNC = false;

            if (!isCreditSale && !isNC)
                return;

            var connectionString = GetConnectionString();

            using (var cn = new SqlConnection(connectionString))
            {
                cn.Open();

                isCreditNC = isNC && ResolveIntegraFastMedioPagos(cn, request.Tenders)
                    .Any(code => string.Equals(code, "99", StringComparison.OrdinalIgnoreCase));

                if (!isCreditSale && !isCreditNC)
                    return;

                var customerAccountNumber = request.CreditAccountNumber;
                if (string.IsNullOrWhiteSpace(customerAccountNumber) && request.CustomerID > 0)
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
                    customerAccountNumber = request.CodCliente;

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

                using (var tx = cn.BeginTransaction())
                {
                    var now = DateTime.Now;
                    var dueDate = now.AddDays(30);
                    var reference = BuildLedgerReference(response.TransactionNumber.ToString(CultureInfo.InvariantCulture));

                    total = LoadPersistedTransactionTotal(cn, tx, response.TransactionNumber, total);
                    if (total <= 0m)
                    {
                        tx.Rollback();
                        return;
                    }

                    // RMH expects transaction-backed AR rows with DocumentType=3.
                    // The ledger type distinguishes invoices (3) from credit notes (4).
                    byte documentType = 3;
                    byte ledgerType = (byte)(isCreditSale ? 3 : 4);
                    decimal amountACY = isCreditSale ? total : -total;

                    // 1. Insert AR_LedgerEntry
                    int ledgerEntryID = 0;
                    using (var cmd = new SqlCommand("dbo.OFF_AR_LEDGERENTRY_INSERT", cn, tx))
                    {
                        cmd.CommandType = CommandType.StoredProcedure;
                        cmd.CommandTimeout = 60;

                        cmd.Parameters.AddWithValue("@LastUpdated", now);
                        cmd.Parameters.AddWithValue("@AccountID", accountID);
                        cmd.Parameters.AddWithValue("@CustomerID", customerID);
                        cmd.Parameters.AddWithValue("@StoreID", request.StoreID);
                        cmd.Parameters.AddWithValue("@LinkType", (byte)1);
                        cmd.Parameters.AddWithValue("@LinkID", customerID);
                        cmd.Parameters.AddWithValue("@AuditEntryID", 0);
                        cmd.Parameters.AddWithValue("@DocumentType", documentType);
                        cmd.Parameters.AddWithValue("@DocumentID", response.TransactionNumber);
                        cmd.Parameters.AddWithValue("@PostingDate", now);
                        cmd.Parameters.AddWithValue("@DueDate", dueDate);
                        cmd.Parameters.AddWithValue("@LedgerType", ledgerType);
                        cmd.Parameters.AddWithValue("@Reference", reference);
                        cmd.Parameters.AddWithValue("@Description", isCreditSale ? "Venta a crÃƒÂ©dito" : "Nota de crÃƒÂ©dito");
                        cmd.Parameters.AddWithValue("@CurrencyID", 0);
                        cmd.Parameters.AddWithValue("@CurrencyFactor", 1.0);
                        cmd.Parameters.AddWithValue("@Positive", true);
                        cmd.Parameters.AddWithValue("@ClosingDate", DBNull.Value);
                        cmd.Parameters.AddWithValue("@ReasonID", 0);
                        cmd.Parameters.AddWithValue("@HoldReasonID", 0);
                        cmd.Parameters.AddWithValue("@UndoReasonID", 0);
                        cmd.Parameters.AddWithValue("@PayMethodID", 0);
                        cmd.Parameters.AddWithValue("@TransactionID", 0);
                        cmd.Parameters.AddWithValue("@ExtReference", isCreditNC ? NormalizeTransactionReference(request.NC_REFERENCIA) : response.TransactionNumber.ToString(CultureInfo.InvariantCulture));
                        cmd.Parameters.AddWithValue("@Comment", isCreditNC ? ("NC aplicada a " + BuildLedgerReference(request.NC_REFERENCIA)) : string.Empty);

                        using (var reader = cmd.ExecuteReader())
                        {
                            if (reader.Read())
                                ledgerEntryID = Convert.ToInt32(reader["ID"]);
                        }
                    }

                    if (ledgerEntryID <= 0)
                    {
                        tx.Rollback();
                        return;
                    }

                    // 2. Insert AR_LedgerEntryDetail
                    using (var cmd = new SqlCommand("dbo.OFF_AR_LEDGERENTRYDETAIL_INSERT", cn, tx))
                    {
                        cmd.CommandType = CommandType.StoredProcedure;
                        cmd.CommandTimeout = 60;

                        cmd.Parameters.AddWithValue("@LedgerEntryID", ledgerEntryID);
                        cmd.Parameters.AddWithValue("@AccountID", accountID);
                        cmd.Parameters.AddWithValue("@LedgerType", ledgerType);
                        cmd.Parameters.AddWithValue("@DueDate", dueDate);
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

                    if (isCreditNC)
                    {
                        int referencedLedgerEntryID = ResolveReferencedLedgerEntryID(cn, tx, accountID, request.NC_REFERENCIA);
                        if (referencedLedgerEntryID > 0)
                        {
                            var sourceSnapshot = LoadLedgerBalanceSnapshot(cn, tx, ledgerEntryID);
                            var targetSnapshot = LoadLedgerBalanceSnapshot(cn, tx, referencedLedgerEntryID);
                            var sourceRemaining = Math.Max(0m, Math.Abs(sourceSnapshot.BaseAmount) - sourceSnapshot.AppliedByEntry);
                            var targetBalance = Math.Max(0m, targetSnapshot.BaseAmount + targetSnapshot.AppliedToEntry);
                            var amountToApply = Math.Min(sourceRemaining, targetBalance);

                            if (amountToApply > LedgerClosingTolerance)
                            {
                                InsertLedgerApplicationDetail(
                                    cn,
                                    tx,
                                    ledgerEntryID,
                                    accountID,
                                    ledgerType,
                                    now,
                                    reference,
                                    referencedLedgerEntryID,
                                    -amountToApply);

                                RefreshLedgerApplicationStatuses(cn, tx, ledgerEntryID, referencedLedgerEntryID, now);
                            }
                        }
                    }

                    tx.Commit();
                }
            }
        }

        
    }
}