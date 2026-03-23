using System;
using System.Collections.Generic;
using System.Configuration;
using System.Data;
using System.Data.SqlClient;
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
                                cmd.Parameters.AddWithValue("@Type", request.Type == 2 ? 2 : 3);
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
                                cmd.Parameters.AddWithValue("@ReferenceNumber", request.ReferenceNumber ?? string.Empty);
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
                            response.Message = request.Type == 2
                                ? "Factura en espera creada exitosamente."
                                : "Cotización creada exitosamente.";
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
                response.Message = $"[{GetConnectionTarget(connectionString)}] {ex.Message}";
                return Request.CreateResponse(HttpStatusCode.BadRequest, response);
            }
            catch (Exception ex)
            {
                response.Ok = false;
                response.Message = ex.Message;
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
                                WHERE ID = @OrderID AND Closed = 0", cn, tx))
                            {
                                cmd.CommandTimeout = 60;
                                cmd.Parameters.AddWithValue("@OrderID", request.OrderID);
                                cmd.Parameters.AddWithValue("@Comment", request.Comment ?? string.Empty);
                                cmd.Parameters.AddWithValue("@CustomerID", request.CustomerID);
                                cmd.Parameters.AddWithValue("@ShipToID", request.ShipToID);
                                cmd.Parameters.AddWithValue("@Tax", request.Tax);
                                cmd.Parameters.AddWithValue("@Total", request.Total);
                                cmd.Parameters.AddWithValue("@LastUpdated", now);
                                cmd.Parameters.AddWithValue("@ExpirationOrDueDate", expiration);
                                cmd.Parameters.AddWithValue("@Taxable", request.Taxable);
                                cmd.Parameters.AddWithValue("@SalesRepID", request.SalesRepID);
                                cmd.Parameters.AddWithValue("@ReferenceNumber", request.ReferenceNumber ?? string.Empty);
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
                            response.Message = request.Type == 2
                                ? "Factura en espera actualizada exitosamente."
                                : "Cotización actualizada exitosamente.";
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
                response.Message = $"[{GetConnectionTarget(connectionString)}] {ex.Message}";
                return Request.CreateResponse(HttpStatusCode.BadRequest, response);
            }
            catch (Exception ex)
            {
                response.Ok = false;
                response.Message = ex.Message;
                return Request.CreateResponse(HttpStatusCode.InternalServerError, response);
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
                return Request.CreateResponse(HttpStatusCode.InternalServerError, new NovaRetailListOrdersResponse
                {
                    Ok = false,
                    Message = ex.Message
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
                               oe.Cost, oe.QuantityOnOrder, oe.Taxable,
                               ISNULL(i.TaxID, 0) AS TaxID
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
                                    Taxable = reader["Taxable"] != DBNull.Value && Convert.ToInt32(reader["Taxable"]) != 0,
                                    TaxID = reader["TaxID"] == DBNull.Value ? 0 : Convert.ToInt32(reader["TaxID"])
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
                return Request.CreateResponse(HttpStatusCode.InternalServerError, new NovaRetailOrderDetailResponse
                {
                    Ok = false,
                    Message = ex.Message
                });
            }
        }
        }
    }
