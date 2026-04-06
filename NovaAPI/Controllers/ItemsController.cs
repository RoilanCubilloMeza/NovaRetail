using System;
using System.Collections.Generic;
using System.Configuration;
using System.Linq;
using System.Web.Http;

namespace NovaAPI.Controllers
{
    public class ItemsController : ApiController
    {
        //readonly LINQDataContext db = new LINQDataContext();
        readonly RMHCDataContext db = new RMHCDataContext(ConfigurationManager.ConnectionStrings["RMHPOS"].ConnectionString);

        [HttpGet]
        public IEnumerable<ProductSearchDto> Get(int storeid, int tipo, int page = 1, int pageSize = 200)
        {
            try
            {
                var safePage = page < 1 ? 1 : page;
                var safePageSize = pageSize < 1 ? 200 : (pageSize > 500 ? 500 : pageSize);
                var skip = (safePage - 1) * safePageSize;

                var items = db.ExecuteQuery<spWS_GetProductsResult>("EXEC dbo.spWS_GetProducts {0}", storeid)
                             .Skip(skip)
                             .Take(safePageSize)
                             .ToList();

                var ids = items.Select(i => i.ID).ToList();
                var itemTypes = GetItemTypesMap(ids);

                return items.Select(item => new ProductSearchDto
                {
                    ID = item.ID,
                    ItemLookupCode = item.ItemLookupCode,
                    Description = item.Description,
                    ExtendedDescription = item.ExtendedDescription,
                    Quantity = item.Quantity ?? 0,
                    DepartmentID = item.DepartmentID,
                    CategoryID = item.CategoryID,
                    PRICE = item.PRICE,
                    PriceA = item.PriceA,
                    PriceB = item.PriceB,
                    PriceC = item.PriceC,
                    TaxID = item.TaxID,
                    Cost = item.Cost,
                    SubDescription1 = item.SubDescription1,
                    SubDescription2 = item.SubDescription2,
                    SubDescription3 = item.SubDescription3,
                    WebItem = item.WebItem,
                    Percentage = item.Percentage,
                    ItemType = itemTypes.TryGetValue(item.ID, out var it) ? it : 0
                });
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
                var total = db.ExecuteQuery<spWS_GetProductsResult>("EXEC dbo.spWS_GetProducts {0}", storeid)
                              .Count();
                return Ok(new { Total = total });
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
        [Route("api/Items/Search")]
        public IEnumerable<ProductSearchDto> Search(string criteria, int top = 300)
        {
            try
            {
                var safeTop = top < 1 ? 100 : (top > 1000 ? 1000 : top);
                var taxes = db.ExecuteQuery<TaxDto>("SELECT ID, Percentage FROM Tax")
                    .ToDictionary(t => t.ID, t => t.Percentage);

                var items = SmartSearch(criteria, safeTop);

                var ids = items.Select(i => i.ID).ToList();
                var itemTypes = GetItemTypesMap(ids);

                return items.Select(item => new ProductSearchDto
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
                        Percentage = taxes.TryGetValue(item.TaxID, out var percentage) ? percentage : 0f,
                        ItemType = itemTypes.TryGetValue(item.ID, out var it) ? it : 0
                    });
            }
            catch
            {
                return new List<ProductSearchDto>();
            }
        }

        private List<spWS_GetProductsbyCriteriaResult> SmartSearch(string criteria, int top)
        {
            if (string.IsNullOrWhiteSpace(criteria))
                return new List<spWS_GetProductsbyCriteriaResult>();

            var words = criteria.Trim()
                .Split(new[] { ' ' }, StringSplitOptions.RemoveEmptyEntries);

            if (words.Length == 0)
                return new List<spWS_GetProductsbyCriteriaResult>();

            // If single word, use the original stored procedure for compatibility
            if (words.Length == 1)
                return db.spWS_GetProductsbyCriteria(criteria).Take(top).ToList();

            var connectionString = ConfigurationManager.ConnectionStrings["RMHPOS"]?.ConnectionString;
            if (string.IsNullOrWhiteSpace(connectionString))
                return db.spWS_GetProductsbyCriteria(criteria).Take(top).ToList();

            var results = new List<spWS_GetProductsbyCriteriaResult>();
            var searchFields = new[] { "Description", "ItemLookupCode", "ExtendedDescription",
                                       "SubDescription1", "SubDescription2", "SubDescription3" };

            // Para queries de 3+ palabras se permite 1 palabra sin match (e.g. "coca cola 3 litros"
            // encuentra "COCA COLA 3L" aunque "litros" no aparezca literalmente).
            var minMatch = words.Length <= 2 ? words.Length : words.Length - 1;

            using (var cn = new System.Data.SqlClient.SqlConnection(connectionString))
            {
                cn.Open();
                using (var cmd = new System.Data.SqlClient.SqlCommand())
                {
                    cmd.Connection = cn;

                    // Construir expresión de puntaje: cada palabra suma 1 si aparece en algún campo.
                    // COLLATE CI_AI garantiza búsqueda sin distinguir mayúsculas ni acentos.
                    var scoreExpressions = new List<string>();
                    for (int i = 0; i < words.Length; i++)
                    {
                        var paramName = "@w" + i;
                        cmd.Parameters.AddWithValue(paramName, "%" + words[i] + "%");

                        var fieldConditions = string.Join(" OR ",
                            searchFields.Select(f =>
                                $"ISNULL({f}, '') COLLATE SQL_Latin1_General_CP1_CI_AI LIKE {paramName}"));
                        scoreExpressions.Add($"CASE WHEN ({fieldConditions}) THEN 1 ELSE 0 END");
                    }

                    var scoreExpr = string.Join(" + ", scoreExpressions);

                    cmd.CommandText = $@"
                        SELECT TOP (@top)
                            s.ID, s.ItemLookupCode, s.Description, s.Quantity, s.QuantityCommitted,
                            s.DepartmentID, s.CategoryID, s.Price, s.PriceA, s.PriceB, s.PriceC,
                            s.TaxID, s.Cost, s.ExtendedDescription,
                            s.SubDescription1, s.SubDescription2, s.SubDescription3, s.WebItem
                        FROM (
                            SELECT
                                ID, ItemLookupCode, Description, Quantity, QuantityCommitted,
                                DepartmentID, CategoryID, Price, PriceA, PriceB, PriceC,
                                TaxID, Cost, ExtendedDescription,
                                SubDescription1, SubDescription2, SubDescription3, WebItem,
                                {scoreExpr} AS MatchScore
                            FROM dbo.Item
                        ) s
                        WHERE s.MatchScore >= @minMatch
                        ORDER BY s.MatchScore DESC, s.Quantity DESC, s.Description";

                    cmd.Parameters.AddWithValue("@top", top);
                    cmd.Parameters.AddWithValue("@minMatch", minMatch);

                    using (var reader = cmd.ExecuteReader())
                    {
                        while (reader.Read())
                        {
                            results.Add(new spWS_GetProductsbyCriteriaResult
                            {
                                ID = Convert.ToInt32(reader["ID"]),
                                ItemLookupCode = reader["ItemLookupCode"]?.ToString() ?? "",
                                Description = reader["Description"]?.ToString() ?? "",
                                Quantity = reader["Quantity"] != DBNull.Value ? Convert.ToDouble(reader["Quantity"]) : 0,
                                QuantityCommitted = reader["QuantityCommitted"] != DBNull.Value ? Convert.ToDouble(reader["QuantityCommitted"]) : 0,
                                DepartmentID = Convert.ToInt32(reader["DepartmentID"]),
                                CategoryID = Convert.ToInt32(reader["CategoryID"]),
                                Price = reader["Price"] != DBNull.Value ? Convert.ToDecimal(reader["Price"]) : 0,
                                PriceA = reader["PriceA"] != DBNull.Value ? Convert.ToDecimal(reader["PriceA"]) : 0,
                                PriceB = reader["PriceB"] != DBNull.Value ? Convert.ToDecimal(reader["PriceB"]) : 0,
                                PriceC = reader["PriceC"] != DBNull.Value ? Convert.ToDecimal(reader["PriceC"]) : 0,
                                TaxID = Convert.ToInt32(reader["TaxID"]),
                                Cost = reader["Cost"] != DBNull.Value ? Convert.ToDecimal(reader["Cost"]) : 0,
                                ExtendedDescription = reader["ExtendedDescription"]?.ToString() ?? "",
                                SubDescription1 = reader["SubDescription1"]?.ToString() ?? "",
                                SubDescription2 = reader["SubDescription2"]?.ToString() ?? "",
                                SubDescription3 = reader["SubDescription3"]?.ToString() ?? "",
                                WebItem = reader["WebItem"] != DBNull.Value && Convert.ToBoolean(reader["WebItem"])
                            });
                        }
                    }
                }
            }

            return results;
        }

        private Dictionary<int, int> GetItemTypesMap(List<int> ids)
        {
            if (ids == null || ids.Count == 0)
                return new Dictionary<int, int>();

            try
            {
                var connectionString = ConfigurationManager.ConnectionStrings["RMHPOS"]?.ConnectionString;
                if (string.IsNullOrWhiteSpace(connectionString))
                    return new Dictionary<int, int>();

                var map = new Dictionary<int, int>();
                using (var cn = new System.Data.SqlClient.SqlConnection(connectionString))
                {
                    cn.Open();
                    // Build parameterized IN clause
                    var parameters = new List<string>();
                    var cmd = new System.Data.SqlClient.SqlCommand();
                    cmd.Connection = cn;
                    for (int i = 0; i < ids.Count; i++)
                    {
                        var paramName = "@id" + i;
                        parameters.Add(paramName);
                        cmd.Parameters.AddWithValue(paramName, ids[i]);
                    }
                    cmd.CommandText = "SELECT ID, ItemType FROM dbo.Item WHERE ID IN (" + string.Join(",", parameters) + ")";

                    using (var reader = cmd.ExecuteReader())
                    {
                        while (reader.Read())
                        {
                            var id = Convert.ToInt32(reader["ID"]);
                            var itemType = reader["ItemType"] != DBNull.Value ? Convert.ToInt32(reader["ItemType"]) : 0;
                            map[id] = itemType;
                        }
                    }
                }
                return map;
            }
            catch
            {
                return new Dictionary<int, int>();
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
