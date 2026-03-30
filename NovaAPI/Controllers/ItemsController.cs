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
        public IEnumerable<spWS_GetProductsResult> Get(int storeid, int tipo, int page = 1, int pageSize = 200)
        {
            try
            {
                var safePage = page < 1 ? 1 : page;
                var safePageSize = pageSize < 1 ? 200 : (pageSize > 500 ? 500 : pageSize);
                var skip = (safePage - 1) * safePageSize;

                return db.ExecuteQuery<spWS_GetProductsResult>("EXEC dbo.spWS_GetProducts {0}", storeid)
                         .Skip(skip)
                         .Take(safePageSize);
            }
            catch
            {
                return new List<spWS_GetProductsResult>();
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

                return db.spWS_GetProductsbyCriteria(criteria)
                    .Take(safeTop)
                    .ToList()
                    .Select(item => new ProductSearchDto
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
                        Percentage = taxes.TryGetValue(item.TaxID, out var percentage) ? percentage : 0f
                    });
            }
            catch
            {
                return new List<ProductSearchDto>();
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
    }

    public class TaxDto
    {
        public int ID { get; set; }
        public float Percentage { get; set; }
    }
}
