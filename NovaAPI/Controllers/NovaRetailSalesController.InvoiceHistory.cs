using System;
using System.Collections.Generic;
using System.Data.SqlClient;
using System.Net;
using System.Net.Http;
using System.Web.Http;
using NovaAPI.Models;

namespace NovaAPI.Controllers
{
    public partial class NovaRetailSalesController
    {
        [HttpGet]
        [Route("invoice-history")]
        public HttpResponseMessage InvoiceHistory(string search = "", int top = 100)
        {
            var connectionString = GetConnectionString();
            try
            {
                var entries = new List<NovaRetailInvoiceHistoryEntryDto>();
                var effectiveTop = top <= 0 ? 100 : top > 500 ? 500 : top;
                var effectiveSearch = (search ?? string.Empty).Trim();

                using (var cn = new SqlConnection(connectionString))
                {
                    cn.Open();

                    string sql;
                    if (effectiveSearch.Length == 0)
                    {
                        sql = @"
;WITH TopTx AS (
    SELECT TOP (@Top)
        t.TransactionNumber,
        t.[Time],
        t.Total,
        t.SalesTax,
        t.CustomerID
    FROM dbo.[Transaction] t
    ORDER BY t.[Time] DESC
)
SELECT
    tt.TransactionNumber,
    CAST(tt.[Time] AS datetime) AS [Date],
    ISNULL(NULLIF(f.COMPROBANTE_TIPO, ''), '04') AS ComprobanteTipo,
    ISNULL(f.CLAVE50, '') AS Clave50,
    ISNULL(NULLIF(f.CLAVE20, ''), ISNULL(f.COMPROBANTE_INTERNO, '')) AS Consecutivo,
    ISNULL(NULLIF(f.CEDULA_TRIBUTARIA, ''), ISNULL(NULLIF(c.AccountNumber, ''), '')) AS ClientId,
    COALESCE(
        NULLIF(f.NOMBRE_CLIENTE, ''),
        NULLIF(LTRIM(RTRIM(ISNULL(c.FirstName, '') + ' ' + ISNULL(c.LastName, ''))), ''),
        'CLIENTE CONTADO') AS ClientName,
    CAST(0 AS INT) AS RegisterNumber,
    CAST(ISNULL(tt.Total, 0) - ISNULL(tt.SalesTax, 0) AS decimal(18, 2)) AS SubtotalColones,
    CAST(0 AS decimal(18, 2)) AS DiscountColones,
    CAST(0 AS decimal(18, 2)) AS ExonerationColones,
    CAST(ISNULL(tt.SalesTax, 0) AS decimal(18, 2)) AS TaxColones,
    CAST(ISNULL(tt.Total, 0) AS decimal(18, 2)) AS TotalColones,
    ISNULL(c.AccountNumber, '') AS CreditAccountNumber
FROM TopTx tt
LEFT JOIN dbo.AVS_INTEGRAFAST_01 f ON f.TRANSACTIONNUMBER = CAST(tt.TransactionNumber AS NVARCHAR(50))
LEFT JOIN dbo.Customer c ON c.ID = tt.CustomerID
ORDER BY tt.[Time] DESC";
                    }
                    else
                    {
                        sql = @"
;WITH FilteredTx AS (
    SELECT TOP (@Top)
        t.TransactionNumber,
        t.[Time],
        t.Total,
        t.SalesTax,
        t.CustomerID
    FROM dbo.[Transaction] t
    LEFT JOIN dbo.AVS_INTEGRAFAST_01 f ON f.TRANSACTIONNUMBER = CAST(t.TransactionNumber AS NVARCHAR(50))
    LEFT JOIN dbo.Customer c ON c.ID = t.CustomerID
    WHERE CAST(t.TransactionNumber AS NVARCHAR(50)) LIKE '%' + @Search + '%'
        OR ISNULL(f.CEDULA_TRIBUTARIA, '') LIKE '%' + @Search + '%'
        OR ISNULL(f.NOMBRE_CLIENTE, '') LIKE '%' + @Search + '%'
        OR ISNULL(c.AccountNumber, '') LIKE '%' + @Search + '%'
        OR LTRIM(RTRIM(ISNULL(c.FirstName, '') + ' ' + ISNULL(c.LastName, ''))) LIKE '%' + @Search + '%'
        OR ISNULL(f.CLAVE20, '') LIKE '%' + @Search + '%'
        OR ISNULL(f.COMPROBANTE_INTERNO, '') LIKE '%' + @Search + '%'
        OR ISNULL(f.CLAVE50, '') LIKE '%' + @Search + '%'
    ORDER BY t.[Time] DESC
)
SELECT
    ft.TransactionNumber,
    CAST(ft.[Time] AS datetime) AS [Date],
    ISNULL(NULLIF(f2.COMPROBANTE_TIPO, ''), '04') AS ComprobanteTipo,
    ISNULL(f2.CLAVE50, '') AS Clave50,
    ISNULL(NULLIF(f2.CLAVE20, ''), ISNULL(f2.COMPROBANTE_INTERNO, '')) AS Consecutivo,
    ISNULL(NULLIF(f2.CEDULA_TRIBUTARIA, ''), ISNULL(NULLIF(c2.AccountNumber, ''), '')) AS ClientId,
    COALESCE(
        NULLIF(f2.NOMBRE_CLIENTE, ''),
        NULLIF(LTRIM(RTRIM(ISNULL(c2.FirstName, '') + ' ' + ISNULL(c2.LastName, ''))), ''),
        'CLIENTE CONTADO') AS ClientName,
    CAST(0 AS INT) AS RegisterNumber,
    CAST(ISNULL(ft.Total, 0) - ISNULL(ft.SalesTax, 0) AS decimal(18, 2)) AS SubtotalColones,
    CAST(0 AS decimal(18, 2)) AS DiscountColones,
    CAST(0 AS decimal(18, 2)) AS ExonerationColones,
    CAST(ISNULL(ft.SalesTax, 0) AS decimal(18, 2)) AS TaxColones,
    CAST(ISNULL(ft.Total, 0) AS decimal(18, 2)) AS TotalColones,
    ISNULL(c2.AccountNumber, '') AS CreditAccountNumber
FROM FilteredTx ft
LEFT JOIN dbo.AVS_INTEGRAFAST_01 f2 ON f2.TRANSACTIONNUMBER = CAST(ft.TransactionNumber AS NVARCHAR(50))
LEFT JOIN dbo.Customer c2 ON c2.ID = ft.CustomerID
ORDER BY ft.[Time] DESC
OPTION (RECOMPILE)";
                    }

                    using (var cmd = new SqlCommand(sql, cn))
                    {
                        cmd.CommandTimeout = 60;
                        cmd.Parameters.AddWithValue("@Top", effectiveTop);
                        if (effectiveSearch.Length > 0)
                            cmd.Parameters.AddWithValue("@Search", effectiveSearch);

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
    ROW_NUMBER() OVER (ORDER BY te.ID) AS LineNumber,
    ISNULL(te.ItemID, 0) AS ItemID,
    ISNULL(i.TaxID, 0) AS TaxID,
    ISNULL(NULLIF(i.Description, ''), 'Art\u00edculo') AS DisplayName,
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
                                    LineNumber = reader["LineNumber"] == DBNull.Value ? entry.Lines.Count + 1 : Convert.ToInt32(reader["LineNumber"]),
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
    }
}
