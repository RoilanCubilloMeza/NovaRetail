using System;
using System.Collections.Generic;
using System.Configuration;
using System.Data;
using System.Data.SqlClient;
using System.Linq;
using System.Net;
using System.Net.Http;
using System.Web.Http;
using NovaAPI.Models;

namespace NovaAPI.Controllers
{
    [RoutePrefix("api/NovaRetailSales")]
    public class NovaRetailSalesController : ApiController
    {
        private static string GetConnectionString()
        {
            var connectionString = ConfigurationManager.ConnectionStrings["RMHPOS"]?.ConnectionString;
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

            try
            {
                using (var cn = new SqlConnection(connectionString))
                {
                    cn.Open();

                    var activeBatch = ResolveActiveBatch(cn, request.StoreID, request.RegisterID);
                    if (activeBatch == null)
                    {
                        response.Ok = false;
                        response.Message = "No existe un lote/caja abierto para registrar la venta.";
                        return Request.CreateResponse(HttpStatusCode.BadRequest, response);
                    }

                    request.StoreID = activeBatch.StoreID;
                    request.RegisterID = activeBatch.RegisterID;

                    using (var cmd = new SqlCommand("dbo.spNovaRetail_CreateSale", cn))
                    {
                        cmd.CommandType = CommandType.StoredProcedure;
                        cmd.CommandTimeout = 180;

                        cmd.Parameters.AddWithValue("@StoreID", request.StoreID);
                        cmd.Parameters.AddWithValue("@RegisterID", request.RegisterID);
                        cmd.Parameters.AddWithValue("@CashierID", request.CashierID);
                        cmd.Parameters.AddWithValue("@CustomerID", request.CustomerID);
                        cmd.Parameters.AddWithValue("@ShipToID", request.ShipToID);
                        cmd.Parameters.AddWithValue("@Comment", request.Comment ?? string.Empty);
                        cmd.Parameters.AddWithValue("@ReferenceNumber", request.ReferenceNumber ?? string.Empty);
                        cmd.Parameters.AddWithValue("@Status", request.Status);
                        cmd.Parameters.AddWithValue("@ExchangeID", request.ExchangeID);
                        cmd.Parameters.AddWithValue("@ChannelType", request.ChannelType);
                        cmd.Parameters.AddWithValue("@RecallID", request.RecallID);
                        cmd.Parameters.AddWithValue("@RecallType", request.RecallType);
                        cmd.Parameters.AddWithValue("@TransactionTime", (object)request.TransactionTime ?? DBNull.Value);
                        cmd.Parameters.AddWithValue("@TotalChange", request.TotalChange);
                        cmd.Parameters.AddWithValue("@AllowNegativeInventory", request.AllowNegativeInventory);
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
                                EnsureClaves(request, response.TransactionNumber, cn);
                                EnsureTiqueteEspera(cn, request, response.TransactionNumber);
                                response.TiqueteEsperaOk = true;
                            }
                            catch (Exception exTiquete)
                            {
                                response.TiqueteEsperaOk = false;
                                response.Warnings.Add($"TiqueteEspera: {exTiquete.Message}");
                            }
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
                response.Ok = false;
                response.Message = $"[{GetConnectionTarget(connectionString)}] {ex.Message}";
                response.ErrorNumber = ex.Number;
                response.ErrorProcedure = ex.Procedure ?? string.Empty;
                response.ErrorLine = ex.LineNumber;

                return Request.CreateResponse(HttpStatusCode.BadRequest, response);
            }
            catch (Exception ex)
            {
                response.Ok = false;
                response.Message = ex.Message;

                return Request.CreateResponse(HttpStatusCode.InternalServerError, response);
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

        private static ActiveBatchInfo ResolveActiveBatch(SqlConnection cn, int requestedStoreId, int requestedRegisterId)
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
            var tipoCvta  = "01";  // Factura Electrónica
            var consec    = transactionNumber.ToString().PadLeft(10, '0');
            var situacion = "1";   // Normal
            var seguridad = new Random().Next(10000000, 99999999).ToString("D8");

            if (string.IsNullOrWhiteSpace(request.CLAVE20))
                request.CLAVE20 = sucursal + terminal + tipoCvta + consec;

            if (string.IsNullOrWhiteSpace(request.CLAVE50))
                request.CLAVE50 = "506" + ddmmaa + cedulaPad + sucursal + terminal + tipoCvta + consec + situacion + seguridad;
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
                var taxableAmount = taxSystem == 1
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

            using (var cmd = new SqlCommand("dbo.spAVS_InsertTiqueteEspera", cn))
            {
                cmd.CommandType = CommandType.StoredProcedure;
                cmd.CommandTimeout = 120;
                cmd.Parameters.AddWithValue("@CLAVE50", request.CLAVE50 ?? string.Empty);
                cmd.Parameters.AddWithValue("@CLAVE20", request.CLAVE20 ?? string.Empty);
                cmd.Parameters.AddWithValue("@TRANSACTIONNUMBER", transactionNumber.ToString());
                cmd.Parameters.AddWithValue("@COD_SUCURSAL", request.COD_SUCURSAL ?? string.Empty);
                cmd.Parameters.AddWithValue("@TERMINAL_POS", request.TERMINAL_POS ?? string.Empty);
                cmd.Parameters.AddWithValue("@COMPROBANTE_INTERNO", string.IsNullOrWhiteSpace(request.COMPROBANTE_INTERNO) ? transactionNumber.ToString() : request.COMPROBANTE_INTERNO);
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
                cmd.Parameters.AddWithValue("@TIPOCAMBIO", request.TipoCambio ?? "1");
                cmd.Parameters.AddWithValue("@CEDULA_TRIBUTARIA", request.CedulaTributaria ?? string.Empty);
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
    }
}
