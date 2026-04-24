using System;
using System.Collections.Generic;
using System.Data;
using System.Data.SqlClient;
using System.Linq;
using System.Net;
using System.Net.Http;
using System.Web.Http;
using NovaAPI.Models;

namespace NovaAPI.Controllers
{
    public partial class NovaRetailSalesController
    {
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
                    Message = "Error interno al listar \u00F3rdenes."
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
                    Message = "Se requiere un OrderID v\u00E1lido."
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
                                response.Message = $"No se encontr\u00F3 la orden de trabajo #{orderId} o ya estaba cerrada.";
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
                    Message = "Se requiere un OrderID v\u00E1lido."
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
                        response.Message = $"No se encontr\u00F3 la cotizaci\u00F3n #{orderId} o ya estaba cerrada.";
                        return Request.CreateResponse(HttpStatusCode.BadRequest, response);
                    }
                }

                response.Ok = true;
                response.OrderID = orderId;
                response.Message = "Cotizaci\u00F3n cancelada y marcada como cerrada.";
                return Request.CreateResponse(HttpStatusCode.OK, response);
            }
            catch (SqlException ex)
            {
                response.Ok = false;
                response.Message = "Error de base de datos al cancelar cotizaci\u00F3n.";
                LogError(ex);
                return Request.CreateResponse(HttpStatusCode.BadRequest, response);
            }
            catch (Exception ex)
            {
                response.Ok = false;
                response.Message = "Error interno al cancelar cotizaci\u00F3n.";
                LogError(ex);
                return Request.CreateResponse(HttpStatusCode.InternalServerError, response);
            }
        }
    }
}
