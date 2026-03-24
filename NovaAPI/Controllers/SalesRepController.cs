using System;
using System.Collections.Generic;
using System.Configuration;
using System.Data.SqlClient;
using System.Linq;
using System.Net;
using System.Web.Http;

public class SalesRepController : ApiController
{
    [HttpGet]
    [Route("api/SalesRep/Get")]
    public IHttpActionResult Get()
    {
        var connectionString = ConfigurationManager.ConnectionStrings["RMHPOS"]?.ConnectionString;
        if (string.IsNullOrWhiteSpace(connectionString))
            return Content(HttpStatusCode.InternalServerError, "Connection string RMHPOS not configured.");

        try
        {
            var list = new List<SalesRepDto>();

            using (var cn = new SqlConnection(connectionString))
            {
                cn.Open();
                var columns = GetTableColumns(cn, "SalesRep");

                if (columns.Count == 0)
                    return Ok(list);

                var idCol      = FirstExisting(columns, "ID", "SalesRepID", "RepID");
                var numberCol  = FirstExisting(columns, "Number", "Code", "SalesRepNumber");
                var firstCol   = FirstExisting(columns, "FirstName", "Name", "Description", "FullName");
                var lastCol    = FirstExisting(columns, "LastName", "LastName2");
                var activeCol  = FirstExisting(columns, "Inactive", "Active", "Disabled");

                if (string.IsNullOrEmpty(idCol) || string.IsNullOrEmpty(firstCol))
                    return Ok(list);

                // Build nombre: FirstName + LastName if both exist
                var nombreExpr = string.IsNullOrEmpty(lastCol)
                    ? $"ISNULL(LTRIM(RTRIM([{firstCol}])),'')"
                    : $"LTRIM(RTRIM(ISNULL([{firstCol}],'')+' '+ISNULL([{lastCol}],'')))";

                // Active filter
                string whereClause;
                if (!string.IsNullOrEmpty(activeCol))
                {
                    // If column is "Inactive" → filter Inactive=0; if "Active" → filter Active=1
                    whereClause = activeCol.Equals("Inactive", StringComparison.OrdinalIgnoreCase) ||
                                  activeCol.Equals("Disabled", StringComparison.OrdinalIgnoreCase)
                        ? $"WHERE [{activeCol}] = 0"
                        : $"WHERE [{activeCol}] = 1";
                }
                else
                {
                    whereClause = string.Empty;
                }

                var numberSelect = string.IsNullOrEmpty(numberCol)
                    ? $"CAST([{idCol}] AS NVARCHAR(20))"
                    : $"ISNULL([{numberCol}],'')";

                var sql = $@"SELECT [{idCol}] AS ID,
                                    {numberSelect} AS Number,
                                    {nombreExpr} AS Nombre
                             FROM dbo.SalesRep
                             {whereClause}
                             ORDER BY [{firstCol}]";

                using (var cmd = new SqlCommand(sql, cn))
                using (var reader = cmd.ExecuteReader())
                {
                    while (reader.Read())
                    {
                        list.Add(new SalesRepDto
                        {
                            ID     = Convert.ToInt32(reader["ID"]),
                            Number = reader["Number"]?.ToString() ?? string.Empty,
                            Nombre = (reader["Nombre"]?.ToString() ?? string.Empty).Trim()
                        });
                    }
                }
            }

            return Ok(list);
        }
        catch (Exception ex)
        {
            return Content(HttpStatusCode.InternalServerError, $"Error al obtener vendedores: {ex.Message}");
        }
    }

    private static List<string> GetTableColumns(SqlConnection cn, string tableName)
    {
        var cols = new List<string>();
        using (var cmd = new SqlCommand(
            "SELECT COLUMN_NAME FROM INFORMATION_SCHEMA.COLUMNS WHERE TABLE_NAME = @t", cn))
        {
            cmd.Parameters.AddWithValue("@t", tableName);
            using (var r = cmd.ExecuteReader())
                while (r.Read())
                    cols.Add(r[0].ToString());
        }
        return cols;
    }

    private static string FirstExisting(IEnumerable<string> columns, params string[] candidates)
        => candidates.FirstOrDefault(c =>
            columns.Any(col => string.Equals(col, c, StringComparison.OrdinalIgnoreCase)));
}

public class SalesRepDto
{
    public int    ID     { get; set; }
    public string Number { get; set; }
    public string Nombre { get; set; }
}
