using System;
using System.Collections.Generic;
using System.Configuration;
using System.Linq;
using System.Text.RegularExpressions;
using System.Web.Http;

namespace NovaAPI.Controllers
{
    public class ItemsController : ApiController
    {
        //readonly LINQDataContext db = new LINQDataContext();
        readonly RMHCDataContext db = new RMHCDataContext(AppConfig.ConnectionString("RMHPOS"));

        [HttpGet]
        public IEnumerable<ProductSearchDto> Get(int storeid, int tipo, int page = 1, int pageSize = 200)
        {
            try
            {
                var safePage = page < 1 ? 1 : page;
                var safePageSize = pageSize < 1 ? 200 : (pageSize > 500 ? 500 : pageSize);
                var skip = (safePage - 1) * safePageSize;

                var connectionString = AppConfig.ConnectionString("RMHPOS");
                if (string.IsNullOrWhiteSpace(connectionString))
                    return new List<ProductSearchDto>();

                var results = new List<ProductSearchDto>();
                using (var cn = new System.Data.SqlClient.SqlConnection(connectionString))
                {
                    cn.Open();
                    using (var cmd = new System.Data.SqlClient.SqlCommand())
                    {
                        cmd.Connection = cn;
                        cmd.CommandText = @"
                            SELECT i.ID, i.ItemLookupCode, i.Description, i.ExtendedDescription,
                                   i.Quantity, i.DepartmentID, i.CategoryID,
                                   i.Price, i.PriceA, i.PriceB, i.PriceC,
                                   i.TaxID, i.Cost,
                                   i.SubDescription1, i.SubDescription2, i.SubDescription3,
                                   i.WebItem, i.ItemType,
                                   ISNULL(t.Percentage, 0) AS Percentage
                            FROM dbo.Item i
                            LEFT JOIN dbo.Tax t ON t.ID = i.TaxID
                            ORDER BY i.Description
                            OFFSET @skip ROWS FETCH NEXT @pageSize ROWS ONLY";
                        cmd.Parameters.AddWithValue("@skip", skip);
                        cmd.Parameters.AddWithValue("@pageSize", safePageSize);

                        using (var reader = cmd.ExecuteReader())
                        {
                            while (reader.Read())
                            {
                                results.Add(new ProductSearchDto
                                {
                                    ID = Convert.ToInt32(reader["ID"]),
                                    ItemLookupCode = reader["ItemLookupCode"]?.ToString() ?? "",
                                    Description = reader["Description"]?.ToString() ?? "",
                                    ExtendedDescription = reader["ExtendedDescription"]?.ToString() ?? "",
                                    Quantity = reader["Quantity"] != DBNull.Value ? Convert.ToDouble(reader["Quantity"]) : 0,
                                    DepartmentID = Convert.ToInt32(reader["DepartmentID"]),
                                    CategoryID = Convert.ToInt32(reader["CategoryID"]),
                                    PRICE = reader["Price"] != DBNull.Value ? Convert.ToDecimal(reader["Price"]) : 0,
                                    PriceA = reader["PriceA"] != DBNull.Value ? Convert.ToDecimal(reader["PriceA"]) : 0,
                                    PriceB = reader["PriceB"] != DBNull.Value ? Convert.ToDecimal(reader["PriceB"]) : 0,
                                    PriceC = reader["PriceC"] != DBNull.Value ? Convert.ToDecimal(reader["PriceC"]) : 0,
                                    TaxID = Convert.ToInt32(reader["TaxID"]),
                                    Cost = reader["Cost"] != DBNull.Value ? Convert.ToDecimal(reader["Cost"]) : 0,
                                    SubDescription1 = reader["SubDescription1"]?.ToString() ?? "",
                                    SubDescription2 = reader["SubDescription2"]?.ToString() ?? "",
                                    SubDescription3 = reader["SubDescription3"]?.ToString() ?? "",
                                    WebItem = reader["WebItem"] != DBNull.Value && Convert.ToBoolean(reader["WebItem"]),
                                    Percentage = reader["Percentage"] != DBNull.Value ? Convert.ToSingle(reader["Percentage"]) : 0f,
                                    ItemType = reader["ItemType"] != DBNull.Value ? Convert.ToInt32(reader["ItemType"]) : 0
                                });
                            }
                        }
                    }
                }
                return results;
            }
            catch
            {
                return new List<ProductSearchDto>();
            }
        }


        [HttpGet]
        [Route("api/Items/Count")]
        public IHttpActionResult Count(int storeid = 1, int tipo = 1)
        {
            try
            {
                var connectionString = AppConfig.ConnectionString("RMHPOS");
                if (string.IsNullOrWhiteSpace(connectionString))
                    return Ok(new { Total = 0 });

                using (var cn = new System.Data.SqlClient.SqlConnection(connectionString))
                {
                    cn.Open();
                    using (var cmd = new System.Data.SqlClient.SqlCommand("SELECT COUNT(*) FROM dbo.Item", cn))
                    {
                        var total = (int)cmd.ExecuteScalar();
                        return Ok(new { Total = total });
                    }
                }
            }
            catch
            {
                return Ok(new { Total = 0 });
            }
        }

        [HttpGet]
        public IEnumerable<spWS_GetProductsbyCriteriaResult> Get(string criteria)
        {
            try
            {
                return db.spWS_GetProductsbyCriteria(criteria);
            }
            catch
            {
                return new List<spWS_GetProductsbyCriteriaResult>();
            }
        }

        [HttpGet]
        [Route("api/Items/{id:int}")]
        public IHttpActionResult GetById(int id)
        {
            try
            {
                if (id <= 0)
                    return NotFound();

                var connectionString = AppConfig.ConnectionString("RMHPOS");
                if (string.IsNullOrWhiteSpace(connectionString))
                    return NotFound();

                using (var cn = new System.Data.SqlClient.SqlConnection(connectionString))
                {
                    cn.Open();
                    using (var cmd = new System.Data.SqlClient.SqlCommand(@"
                            SELECT TOP 1
                                i.ID, i.ItemLookupCode, i.Description, i.ExtendedDescription,
                                i.Quantity, i.DepartmentID, i.CategoryID,
                                i.Price, i.PriceA, i.PriceB, i.PriceC,
                                i.TaxID, i.Cost,
                                i.SubDescription1, i.SubDescription2, i.SubDescription3,
                                i.WebItem, i.ItemType,
                                ISNULL(t.Percentage, 0) AS Percentage
                            FROM dbo.Item i
                            LEFT JOIN dbo.Tax t ON t.ID = i.TaxID
                            WHERE i.ID = @id", cn))
                    {
                        cmd.Parameters.AddWithValue("@id", id);

                        using (var reader = cmd.ExecuteReader())
                        {
                            if (!reader.Read())
                                return NotFound();

                            return Ok(new ProductSearchDto
                            {
                                ID = Convert.ToInt32(reader["ID"]),
                                ItemLookupCode = reader["ItemLookupCode"]?.ToString() ?? "",
                                Description = reader["Description"]?.ToString() ?? "",
                                ExtendedDescription = reader["ExtendedDescription"]?.ToString() ?? "",
                                Quantity = reader["Quantity"] != DBNull.Value ? Convert.ToDouble(reader["Quantity"]) : 0,
                                DepartmentID = Convert.ToInt32(reader["DepartmentID"]),
                                CategoryID = Convert.ToInt32(reader["CategoryID"]),
                                PRICE = reader["Price"] != DBNull.Value ? Convert.ToDecimal(reader["Price"]) : 0,
                                PriceA = reader["PriceA"] != DBNull.Value ? Convert.ToDecimal(reader["PriceA"]) : 0,
                                PriceB = reader["PriceB"] != DBNull.Value ? Convert.ToDecimal(reader["PriceB"]) : 0,
                                PriceC = reader["PriceC"] != DBNull.Value ? Convert.ToDecimal(reader["PriceC"]) : 0,
                                TaxID = Convert.ToInt32(reader["TaxID"]),
                                Cost = reader["Cost"] != DBNull.Value ? Convert.ToDecimal(reader["Cost"]) : 0,
                                SubDescription1 = reader["SubDescription1"]?.ToString() ?? "",
                                SubDescription2 = reader["SubDescription2"]?.ToString() ?? "",
                                SubDescription3 = reader["SubDescription3"]?.ToString() ?? "",
                                WebItem = reader["WebItem"] != DBNull.Value && Convert.ToBoolean(reader["WebItem"]),
                                Percentage = reader["Percentage"] != DBNull.Value ? Convert.ToSingle(reader["Percentage"]) : 0f,
                                ItemType = reader["ItemType"] != DBNull.Value ? Convert.ToInt32(reader["ItemType"]) : 0
                            });
                        }
                    }
                }
            }
            catch
            {
                return NotFound();
            }
        }

        [HttpGet]
        [Route("api/Items/Search")]
        public IEnumerable<ProductSearchDto> Search(string criteria, int top = 300)
            => SearchDirect(criteria, top);

        [HttpGet]
        [Route("api/Items/SearchDirect")]
        public IEnumerable<ProductSearchDto> SearchDirect(string criteria, int top = 300)
        {
            try
            {
                var safeTop = top < 1 ? 100 : (top > 1000 ? 1000 : top);
                var results = DirectProductSearch(criteria, safeTop);
                return results.Count > 0
                    ? results
                    : SpFallbackSearch(criteria, safeTop);
            }
            catch
            {
                return new List<ProductSearchDto>();
            }
        }

        private List<ProductSearchDto> DirectProductSearch(string criteria, int top)
        {
            var words = (criteria ?? string.Empty)
                .Trim()
                .Split(new[] { ' ' }, StringSplitOptions.RemoveEmptyEntries)
                .Where(w => !string.IsNullOrWhiteSpace(w))
                .Take(8)
                .ToArray();

            if (words.Length == 0)
                return new List<ProductSearchDto>();

            var connectionString = AppConfig.ConnectionString("RMHPOS");
            if (string.IsNullOrWhiteSpace(connectionString))
                return new List<ProductSearchDto>();

            var results = new List<ProductSearchDto>();
            var searchFields = new[]
            {
                "i.Description",
                "i.ItemLookupCode",
                "i.ExtendedDescription",
                "i.SubDescription1",
                "i.SubDescription2",
                "i.SubDescription3"
            };

            using (var cn = new System.Data.SqlClient.SqlConnection(connectionString))
            {
                cn.Open();
                using (var cmd = new System.Data.SqlClient.SqlCommand())
                {
                    cmd.Connection = cn;

                    var wordConditions = new List<string>();
                    for (var i = 0; i < words.Length; i++)
                    {
                        var paramName = "@w" + i;
                        cmd.Parameters.AddWithValue(paramName, "%" + words[i] + "%");

                        var fieldConditions = searchFields
                            .Select(f => "ISNULL(" + f + ", '') COLLATE SQL_Latin1_General_CP1_CI_AI LIKE " + paramName);

                        wordConditions.Add("(" + string.Join(" OR ", fieldConditions) + ")");
                    }

                    cmd.CommandText = @"
                        SELECT TOP (@top)
                            i.ID, i.ItemLookupCode, i.Description, i.ExtendedDescription,
                            i.Quantity, i.DepartmentID, i.CategoryID,
                            i.Price, i.PriceA, i.PriceB, i.PriceC,
                            i.TaxID, i.Cost,
                            i.SubDescription1, i.SubDescription2, i.SubDescription3,
                            i.WebItem, i.ItemType,
                            ISNULL(t.Percentage, 0) AS Percentage
                        FROM dbo.Item i
                        LEFT JOIN dbo.Tax t ON t.ID = i.TaxID
                        WHERE " + string.Join(" AND ", wordConditions) + @"
                        ORDER BY
                            CASE WHEN ISNULL(i.Quantity, 0) > 0 THEN 0 ELSE 1 END,
                            i.Description";

                    cmd.Parameters.AddWithValue("@top", top);

                    using (var reader = cmd.ExecuteReader())
                    {
                        while (reader.Read())
                        {
                            results.Add(new ProductSearchDto
                            {
                                ID = Convert.ToInt32(reader["ID"]),
                                ItemLookupCode = reader["ItemLookupCode"]?.ToString() ?? "",
                                Description = reader["Description"]?.ToString() ?? "",
                                ExtendedDescription = reader["ExtendedDescription"]?.ToString() ?? "",
                                Quantity = reader["Quantity"] != DBNull.Value ? Convert.ToDouble(reader["Quantity"]) : 0,
                                DepartmentID = Convert.ToInt32(reader["DepartmentID"]),
                                CategoryID = Convert.ToInt32(reader["CategoryID"]),
                                PRICE = reader["Price"] != DBNull.Value ? Convert.ToDecimal(reader["Price"]) : 0,
                                PriceA = reader["PriceA"] != DBNull.Value ? Convert.ToDecimal(reader["PriceA"]) : 0,
                                PriceB = reader["PriceB"] != DBNull.Value ? Convert.ToDecimal(reader["PriceB"]) : 0,
                                PriceC = reader["PriceC"] != DBNull.Value ? Convert.ToDecimal(reader["PriceC"]) : 0,
                                TaxID = Convert.ToInt32(reader["TaxID"]),
                                Cost = reader["Cost"] != DBNull.Value ? Convert.ToDecimal(reader["Cost"]) : 0,
                                SubDescription1 = reader["SubDescription1"]?.ToString() ?? "",
                                SubDescription2 = reader["SubDescription2"]?.ToString() ?? "",
                                SubDescription3 = reader["SubDescription3"]?.ToString() ?? "",
                                WebItem = reader["WebItem"] != DBNull.Value && Convert.ToBoolean(reader["WebItem"]),
                                Percentage = reader["Percentage"] != DBNull.Value ? Convert.ToSingle(reader["Percentage"]) : 0f,
                                ItemType = reader["ItemType"] != DBNull.Value ? Convert.ToInt32(reader["ItemType"]) : 0
                            });
                        }
                    }
                }
            }

            return results;
        }

        private List<ProductSearchDto> SpFallbackSearch(string criteria, int top)
        {
            try
            {
                var taxes = db.ExecuteQuery<TaxDto>("SELECT ID, Percentage FROM Tax")
                    .ToDictionary(t => t.ID, t => t.Percentage);

                return db.spWS_GetProductsbyCriteria(criteria).Take(top).Select(item => new ProductSearchDto
                {
                    ID = item.ID,
                    ItemLookupCode = item.ItemLookupCode,
                    Description = item.Description,
                    ExtendedDescription = item.ExtendedDescription,
                    Quantity = item.Quantity,
                    DepartmentID = item.DepartmentID,
                    CategoryID = item.CategoryID,
                    PRICE = item.Price,
                    PriceA = item.PriceA,
                    PriceB = item.PriceB,
                    PriceC = item.PriceC,
                    TaxID = item.TaxID,
                    Cost = item.Cost,
                    SubDescription1 = item.SubDescription1,
                    SubDescription2 = item.SubDescription2,
                    SubDescription3 = item.SubDescription3,
                    WebItem = item.WebItem,
                    Percentage = taxes.TryGetValue(item.TaxID, out var pct) ? pct : 0f
                }).ToList();
            }
            catch
            {
                return new List<ProductSearchDto>();
            }
        }

        [HttpGet]
        [Route("api/Items/ByDepartment")]
        public IHttpActionResult ByDepartment(int departmentId, int top = 300)
        {
            try
            {
                var safeTop = top < 1 ? 100 : (top > 1000 ? 1000 : top);
                var connectionString = AppConfig.ConnectionString("RMHPOS");
                if (string.IsNullOrWhiteSpace(connectionString))
                    return Ok(new List<ProductSearchDto>());

                var results = new List<ProductSearchDto>();
                using (var cn = new System.Data.SqlClient.SqlConnection(connectionString))
                {
                    cn.Open();
                    using (var cmd = new System.Data.SqlClient.SqlCommand())
                    {
                        cmd.Connection = cn;
                        cmd.CommandText = @"
                            SELECT TOP (@top)
                                i.ID, i.ItemLookupCode, i.Description, i.ExtendedDescription,
                                i.Quantity, i.DepartmentID, i.CategoryID,
                                i.Price, i.PriceA, i.PriceB, i.PriceC,
                                i.TaxID, i.Cost,
                                i.SubDescription1, i.SubDescription2, i.SubDescription3,
                                i.WebItem, i.ItemType,
                                ISNULL(t.Percentage, 0) AS Percentage
                            FROM dbo.Item i
                            LEFT JOIN dbo.Tax t ON t.ID = i.TaxID
                            WHERE i.DepartmentID = @deptId
                            ORDER BY i.Quantity DESC, i.Description";
                        cmd.Parameters.AddWithValue("@top", safeTop);
                        cmd.Parameters.AddWithValue("@deptId", departmentId);

                        using (var reader = cmd.ExecuteReader())
                        {
                            while (reader.Read())
                            {
                                results.Add(new ProductSearchDto
                                {
                                    ID = Convert.ToInt32(reader["ID"]),
                                    ItemLookupCode = reader["ItemLookupCode"]?.ToString() ?? "",
                                    Description = reader["Description"]?.ToString() ?? "",
                                    ExtendedDescription = reader["ExtendedDescription"]?.ToString() ?? "",
                                    Quantity = reader["Quantity"] != DBNull.Value ? Convert.ToDouble(reader["Quantity"]) : 0,
                                    DepartmentID = Convert.ToInt32(reader["DepartmentID"]),
                                    CategoryID = Convert.ToInt32(reader["CategoryID"]),
                                    PRICE = reader["Price"] != DBNull.Value ? Convert.ToDecimal(reader["Price"]) : 0,
                                    PriceA = reader["PriceA"] != DBNull.Value ? Convert.ToDecimal(reader["PriceA"]) : 0,
                                    PriceB = reader["PriceB"] != DBNull.Value ? Convert.ToDecimal(reader["PriceB"]) : 0,
                                    PriceC = reader["PriceC"] != DBNull.Value ? Convert.ToDecimal(reader["PriceC"]) : 0,
                                    TaxID = Convert.ToInt32(reader["TaxID"]),
                                    Cost = reader["Cost"] != DBNull.Value ? Convert.ToDecimal(reader["Cost"]) : 0,
                                    SubDescription1 = reader["SubDescription1"]?.ToString() ?? "",
                                    SubDescription2 = reader["SubDescription2"]?.ToString() ?? "",
                                    SubDescription3 = reader["SubDescription3"]?.ToString() ?? "",
                                    WebItem = reader["WebItem"] != DBNull.Value && Convert.ToBoolean(reader["WebItem"]),
                                    Percentage = reader["Percentage"] != DBNull.Value ? Convert.ToSingle(reader["Percentage"]) : 0f,
                                    ItemType = reader["ItemType"] != DBNull.Value ? Convert.ToInt32(reader["ItemType"]) : 0
                                });
                            }
                        }
                    }
                }

                return Ok(results);
            }
            catch (Exception ex)
            {
                return InternalServerError(ex);
            }
        }

    }

    public class ProductSearchDto
    {
        public int ID { get; set; }
        public string ItemLookupCode { get; set; }
        public string Description { get; set; }
        public double Quantity { get; set; }
        public int DepartmentID { get; set; }
        public int CategoryID { get; set; }
        public decimal PRICE { get; set; }
        public decimal PriceA { get; set; }
        public decimal PriceB { get; set; }
        public decimal PriceC { get; set; }
        public int TaxID { get; set; }
        public decimal Cost { get; set; }
        public string ExtendedDescription { get; set; }
        public string SubDescription1 { get; set; }
        public string SubDescription2 { get; set; }
        public string SubDescription3 { get; set; }
        public bool WebItem { get; set; }
        public float Percentage { get; set; }
        public int ItemType { get; set; }
    }

    public class TaxDto
    {
        public int ID { get; set; }
        public float Percentage { get; set; }
    }
}
