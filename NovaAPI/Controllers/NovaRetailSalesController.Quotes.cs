using System;
using System.Collections.Generic;
using System.Data.SqlClient;
using System.Linq;
using System.Net;
using System.Net.Http;
using System.Web.Http;
using NovaAPI.Models;
using NovaAPI.Services;

namespace NovaAPI.Controllers
{
    public partial class NovaRetailSalesController
    {
        [HttpPost]
        [Route("create-quote")]
        public HttpResponseMessage CreateQuote([FromBody] NovaRetailCreateQuoteRequest request)
        {
            if (request == null)
            {
                return Request.CreateResponse(HttpStatusCode.BadRequest, new NovaRetailCreateQuoteResponse
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

                return Request.CreateResponse(HttpStatusCode.BadRequest, new NovaRetailCreateQuoteResponse
                {
                    Ok = false,
                    Message = errors.Count == 0
                        ? "Solicitud invÃƒÂ¡lida."
                        : string.Join(" | ", errors)
                });
            }

            if (request.Items == null || request.Items.Count == 0)
            {
                return Request.CreateResponse(HttpStatusCode.BadRequest, new NovaRetailCreateQuoteResponse
                {
                    Ok = false,
                    Message = "La cotizaciÃƒÂ³n no contiene ÃƒÂ­tems."
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
                            response.Message = "CotizaciÃƒÂ³n creada exitosamente.";
                            LogOrderSavedAudit(cn, request, orderID, "QuoteCreated", "Quote");
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
                response.Message = "Error de base de datos al crear cotizaciÃƒÂ³n.";
                LogError(ex);
                return Request.CreateResponse(HttpStatusCode.BadRequest, response);
            }
            catch (Exception ex)
            {
                response.Ok = false;
                response.Message = "Error interno al crear cotizaciÃƒÂ³n.";
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
                    Message = "Se requiere un OrderID vÃƒÂ¡lido para actualizar."
                });
            }

            if (request.Items == null || request.Items.Count == 0)
            {
                return Request.CreateResponse(HttpStatusCode.BadRequest, new NovaRetailCreateQuoteResponse
                {
                    Ok = false,
                    Message = "La cotizaciÃƒÂ³n no contiene ÃƒÂ­tems."
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
                                    response.Message = $"No se encontrÃƒÂ³ la orden #{request.OrderID} o ya estÃƒÂ¡ cerrada.";
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
                            response.Message = "CotizaciÃƒÂ³n actualizada exitosamente.";
                            LogOrderSavedAudit(cn, request, request.OrderID, "OrderModified", "Quote");
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
                response.Message = "Error de base de datos al actualizar cotizaciÃƒÂ³n.";
                LogError(ex);
                return Request.CreateResponse(HttpStatusCode.BadRequest, response);
            }
            catch (Exception ex)
            {
                response.Ok = false;
                response.Message = "Error interno al actualizar cotizaciÃƒÂ³n.";
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

                return Request.CreateResponse(HttpStatusCode.BadRequest, new NovaRetailCreateQuoteResponse
                {
                    Ok = false,
                    Message = errors.Count == 0
                        ? "Solicitud invÃƒÂ¡lida."
                        : string.Join(" | ", errors)
                });
            }

            if (request.Items == null || request.Items.Count == 0)
            {
                return Request.CreateResponse(HttpStatusCode.BadRequest, new NovaRetailCreateQuoteResponse
                {
                    Ok = false,
                    Message = "La orden de trabajo no contiene ÃƒÂ­tems."
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
                            LogOrderSavedAudit(cn, request, orderID, "WorkOrderCreated", "WorkOrder");
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
                    Message = "Se requiere un OrderID vÃƒÂ¡lido para actualizar."
                });
            }

            if (request.Items == null || request.Items.Count == 0)
            {
                return Request.CreateResponse(HttpStatusCode.BadRequest, new NovaRetailCreateQuoteResponse
                {
                    Ok = false,
                    Message = "La orden de trabajo no contiene ÃƒÂ­tems."
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
                                    response.Message = $"No se encontrÃƒÂ³ la orden de trabajo #{request.OrderID} o ya estÃƒÂ¡ cerrada.";
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
                            LogOrderSavedAudit(cn, request, request.OrderID, "OrderModified", "WorkOrder");
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
    }
}
