using System;
using System.Collections.Generic;
using System.Data.SqlClient;
using System.Net;
using System.Net.Http;
using System.Globalization;
using System.Web.Http;
using NovaAPI.Models;
using NovaAPI.Services;

namespace NovaAPI.Controllers
{
    [RoutePrefix("api/NovaRetailManager")]
    public class NovaRetailManagerController : ApiController
    {
        [HttpGet]
        [Route("dashboard")]
        public HttpResponseMessage Dashboard(int storeId = 0, string date = "")
        {
            try
            {
                var selectedDate = ParseBusinessDate(date);
                var tomorrow = selectedDate.AddDays(1);
                var result = new NovaRetailManagerDashboardResponse
                {
                    Ok = true,
                    BusinessDate = selectedDate
                };

                using (var cn = new SqlConnection(AppConfig.ConnectionString("RMHPOS")))
                {
                    cn.Open();

                    using (var cmd = new SqlCommand(@"
SELECT COUNT(1), CAST(ISNULL(SUM(ISNULL(Total, 0)), 0) AS decimal(18,2))
FROM dbo.[Transaction]
WHERE [Time] >= @From AND [Time] < @To
  AND (@StoreID = 0 OR StoreID = @StoreID);", cn))
                    {
                        AddDateRange(cmd, selectedDate, tomorrow, storeId);
                        using (var reader = cmd.ExecuteReader())
                        {
                            if (reader.Read())
                            {
                                result.SalesTodayCount = Convert.ToInt32(reader[0]);
                                result.SalesTodayTotal = reader[1] == DBNull.Value ? 0m : Convert.ToDecimal(reader[1]);
                            }
                        }
                    }

                    result.QuotesCreatedToday = ExecuteInt(cn, @"
SELECT COUNT(1)
FROM dbo.[Order]
WHERE [Type] = 3 AND [Time] >= @From AND [Time] < @To
  AND (@StoreID = 0 OR StoreID = @StoreID);", selectedDate, tomorrow, storeId);

                    result.PendingWorkOrders = ExecuteInt(cn, @"
SELECT COUNT(1)
FROM dbo.[Order]
WHERE [Type] = 2 AND Closed = 0
  AND (@StoreID = 0 OR StoreID = @StoreID);", selectedDate, tomorrow, storeId);

                    result.PaymentsReceivedTodayCount = ExecuteInt(cn, @"
SELECT COUNT(1)
FROM dbo.Payment
WHERE [Time] >= @From AND [Time] < @To
  AND (@StoreID = 0 OR StoreID = @StoreID);", selectedDate, tomorrow, storeId);

                    result.PaymentsReceivedTodayTotal = ExecuteDecimal(cn, @"
SELECT CAST(ISNULL(SUM(ISNULL(Amount, 0)), 0) AS decimal(18,2))
FROM dbo.Payment
WHERE [Time] >= @From AND [Time] < @To
  AND (@StoreID = 0 OR StoreID = @StoreID);", selectedDate, tomorrow, storeId);

                    if (NovaRetailAuditLogger.TableExists(cn))
                    {
                        result.QuotesConvertedToday = ExecuteInt(cn, @"
SELECT COUNT(1)
FROM dbo.NovaRetail_ActionLog
WHERE ActionType = 'QuoteConverted'
  AND ActionDate >= @From AND ActionDate < @To
  AND (@StoreID = 0 OR StoreID = @StoreID);", selectedDate, tomorrow, storeId);
                    }
                }

                return Request.CreateResponse(HttpStatusCode.OK, result);
            }
            catch (Exception ex)
            {
                return Request.CreateResponse(HttpStatusCode.InternalServerError, new NovaRetailManagerDashboardResponse
                {
                    Ok = false,
                    Message = "Error interno al cargar dashboard: " + ex.Message
                });
            }
        }

        [HttpGet]
        [Route("activity-log")]
        public HttpResponseMessage ActivityLog(int storeId = 0, int top = 100, string date = "", string search = "")
        {
            try
            {
                var actions = new List<NovaRetailActionLogEntryDto>();
                var selectedDate = ParseBusinessDate(date);
                var nextDate = selectedDate.AddDays(1);
                var effectiveSearch = (search ?? string.Empty).Trim();
                using (var cn = new SqlConnection(AppConfig.ConnectionString("RMHPOS")))
                {
                    cn.Open();
                    if (!NovaRetailAuditLogger.TableExists(cn))
                    {
                        return Request.CreateResponse(HttpStatusCode.OK, new NovaRetailActionLogResponse
                        {
                            Ok = true,
                            Actions = actions
                        });
                    }

                    using (var cmd = new SqlCommand(@"
SELECT TOP (@Top)
    ID, ActionDate, ActionType, EntityType, EntityID, CashierID, CashierName,
    StoreID, RegisterID, Amount, Detail
FROM dbo.NovaRetail_ActionLog
WHERE (@StoreID = 0 OR StoreID = @StoreID)
  AND ActionDate >= @From AND ActionDate < @To
  AND (
      @Search = ''
      OR ActionType LIKE '%' + @Search + '%'
      OR EntityType LIKE '%' + @Search + '%'
      OR Detail LIKE '%' + @Search + '%'
      OR CashierName LIKE '%' + @Search + '%'
      OR CAST(EntityID AS NVARCHAR(20)) LIKE '%' + @Search + '%'
      OR CAST(CashierID AS NVARCHAR(20)) LIKE '%' + @Search + '%'
  )
ORDER BY ActionDate DESC, ID DESC;", cn))
                    {
                        cmd.Parameters.AddWithValue("@Top", top <= 0 ? 100 : top > 500 ? 500 : top);
                        AddDateRange(cmd, selectedDate, nextDate, storeId);
                        cmd.Parameters.AddWithValue("@Search", effectiveSearch);

                        using (var reader = cmd.ExecuteReader())
                        {
                            while (reader.Read())
                            {
                                actions.Add(new NovaRetailActionLogEntryDto
                                {
                                    ID = Convert.ToInt32(reader["ID"]),
                                    ActionDate = Convert.ToDateTime(reader["ActionDate"]),
                                    ActionType = Convert.ToString(reader["ActionType"]),
                                    EntityType = Convert.ToString(reader["EntityType"]),
                                    EntityID = Convert.ToInt32(reader["EntityID"]),
                                    CashierID = Convert.ToInt32(reader["CashierID"]),
                                    CashierName = Convert.ToString(reader["CashierName"]),
                                    StoreID = Convert.ToInt32(reader["StoreID"]),
                                    RegisterID = Convert.ToInt32(reader["RegisterID"]),
                                    Amount = reader["Amount"] == DBNull.Value ? 0m : Convert.ToDecimal(reader["Amount"]),
                                    Detail = Convert.ToString(reader["Detail"])
                                });
                            }
                        }
                    }
                }

                return Request.CreateResponse(HttpStatusCode.OK, new NovaRetailActionLogResponse
                {
                    Ok = true,
                    Actions = actions
                });
            }
            catch (Exception ex)
            {
                return Request.CreateResponse(HttpStatusCode.InternalServerError, new NovaRetailActionLogResponse
                {
                    Ok = false,
                    Message = "Error interno al cargar historial de acciones: " + ex.Message
                });
            }
        }

        private static int ExecuteInt(SqlConnection cn, string sql, DateTime from, DateTime to, int storeId)
        {
            using (var cmd = new SqlCommand(sql, cn))
            {
                AddDateRange(cmd, from, to, storeId);
                var value = cmd.ExecuteScalar();
                return value == null || value == DBNull.Value ? 0 : Convert.ToInt32(value);
            }
        }

        private static decimal ExecuteDecimal(SqlConnection cn, string sql, DateTime from, DateTime to, int storeId)
        {
            using (var cmd = new SqlCommand(sql, cn))
            {
                AddDateRange(cmd, from, to, storeId);
                var value = cmd.ExecuteScalar();
                return value == null || value == DBNull.Value ? 0m : Convert.ToDecimal(value);
            }
        }

        private static void AddDateRange(SqlCommand cmd, DateTime from, DateTime to, int storeId)
        {
            cmd.Parameters.AddWithValue("@From", from);
            cmd.Parameters.AddWithValue("@To", to);
            cmd.Parameters.AddWithValue("@StoreID", storeId);
        }

        private static DateTime ParseBusinessDate(string value)
        {
            if (DateTime.TryParseExact(value ?? string.Empty, "yyyy-MM-dd", CultureInfo.InvariantCulture, DateTimeStyles.None, out var exact))
                return exact.Date;

            if (DateTime.TryParse(value, CultureInfo.CurrentCulture, DateTimeStyles.None, out var parsed))
                return parsed.Date;

            return DateTime.Today;
        }
    }
}
