using System;
using System.Collections.Generic;
using System.Data;
using System.Data.SqlClient;
using System.Net;
using System.Net.Http;
using System.Web.Http;
using NovaAPI.Models;
using NovaAPI.Services;

namespace NovaAPI.Controllers
{
    public partial class NovaRetailSalesController
    {
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

        [HttpPost]
        [Route("save-hold")]
        public HttpResponseMessage SaveHold([FromBody] NovaRetailCreateQuoteRequest request)
        {
            if (request == null || request.Items == null || request.Items.Count == 0)
                return Request.CreateResponse(HttpStatusCode.BadRequest, new NovaRetailCreateQuoteResponse { Ok = false, Message = "La factura en espera no contiene \u00EDtems." });

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
                                         [Price],[FullPrice],[Cost],[PriceSource],[Comment],[DetailID],[Taxable],[ItemID],
                                         [SalesRepID],[SerialNumber1],[SerialNumber2],[SerialNumber3],[VoucherNumber],
                                         [VoucherExpirationDate],[DiscountReasonCodeID],[ReturnReasonCodeID],
                                         [TaxChangeReasonCodeID],[ItemTaxID],[ComponentQuantityReserved],
                                         [TransactionTime],[IsAddMoney],[VoucherID],[SyncGuid])
                                    VALUES
                                        ('',@StoreID,@HoldID,0,@Description,
                                         0,0,0,@QuantityReserved,
                                         @Price,@FullPrice,@Cost,@PriceSource,@Comment,@DetailID,@Taxable,@ItemID,
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
                                    cmd.Parameters.AddWithValue("@Cost", item.Cost);
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
                            LogOrderSavedAudit(cn, request, holdId, "HoldCreated", "Hold");
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
                return Request.CreateResponse(HttpStatusCode.BadRequest, new NovaRetailCreateQuoteResponse { Ok = false, Message = "Se requiere un HoldID v\u00E1lido para actualizar." });

            if (request.Items == null || request.Items.Count == 0)
                return Request.CreateResponse(HttpStatusCode.BadRequest, new NovaRetailCreateQuoteResponse { Ok = false, Message = "La factura en espera no contiene \u00EDtems." });

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
                                    response.Message = $"No se encontr\u00F3 la factura en espera #{request.OrderID}.";
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
                                         [Price],[FullPrice],[Cost],[PriceSource],[Comment],[DetailID],[Taxable],[ItemID],
                                         [SalesRepID],[SerialNumber1],[SerialNumber2],[SerialNumber3],[VoucherNumber],
                                         [VoucherExpirationDate],[DiscountReasonCodeID],[ReturnReasonCodeID],
                                         [TaxChangeReasonCodeID],[ItemTaxID],[ComponentQuantityReserved],
                                         [TransactionTime],[IsAddMoney],[VoucherID],[SyncGuid])
                                    VALUES
                                        ('',@StoreID,@HoldID,0,@Description,
                                         0,0,0,@QuantityReserved,
                                         @Price,@FullPrice,@Cost,@PriceSource,@Comment,@DetailID,@Taxable,@ItemID,
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
                                    cmd.Parameters.AddWithValue("@Cost", item.Cost);
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
                            LogOrderSavedAudit(cn, request, request.OrderID, "OrderModified", "Hold");
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
                            ISNULL((
                                SELECT SUM(ISNULL(the2.Price, 0) * ISNULL(the2.QuantityReserved, 0))
                                FROM dbo.TransactionHoldEntry the2
                                WHERE the2.TransactionHoldID = th.ID
                            ), 0) AS Total,
                            0 AS Tax, th.TransactionTime AS Time,
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
                                    Total = reader["Total"] == DBNull.Value ? 0m : Convert.ToDecimal(reader["Total"]),
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
                        @"SELECT
                            th.ID,
                            th.HoldComment,
                            th.TransactionTime,
                            ISNULL((
                                SELECT SUM(ISNULL(the2.Price, 0) * ISNULL(the2.QuantityReserved, 0))
                                FROM dbo.TransactionHoldEntry the2
                                WHERE the2.TransactionHoldID = th.ID
                            ), 0) AS Total
                        FROM dbo.TransactionHold th
                        WHERE th.ID = @HoldID", cn))
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
                                    Total = reader["Total"] == DBNull.Value ? 0m : Convert.ToDecimal(reader["Total"]),
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
                               the.Price, the.FullPrice, ISNULL(the.Cost, ISNULL(i.Cost, 0)) AS Cost,
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
                                    Cost = reader["Cost"] == DBNull.Value ? 0m : Convert.ToDecimal(reader["Cost"]),
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

        [HttpDelete]
        [Route("delete-hold/{holdId}")]
        public HttpResponseMessage DeleteHold(int holdId, int cashierId = 0)
        {
            if (holdId <= 0)
            {
                return Request.CreateResponse(HttpStatusCode.BadRequest, new NovaRetailCreateQuoteResponse
                {
                    Ok = false,
                    Message = "Se requiere un HoldID v\u00E1lido."
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
                using (var auditCn = new SqlConnection(connectionString))
                {
                    auditCn.Open();
                    NovaRetailAuditLogger.Log(auditCn, "OrderCanceled", "Hold", holdId, cashierId, 0, 0, 0m, $"Factura en espera #{holdId} eliminada");
                }
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
