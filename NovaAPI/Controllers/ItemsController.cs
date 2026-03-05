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
            var safePage = page < 1 ? 1 : page;
            var safePageSize = pageSize < 1 ? 200 : (pageSize > 500 ? 500 : pageSize);
            var skip = (safePage - 1) * safePageSize;

            return db.ExecuteQuery<spWS_GetProductsResult>("EXEC dbo.spWS_GetProducts {0}", storeid)
                     .Skip(skip)
                     .Take(safePageSize);

        }


        [HttpGet]
        public IEnumerable<spWS_GetProductsbyCriteriaResult> Get(string criteria)
        {
            return db.spWS_GetProductsbyCriteria(criteria);
        }

        [HttpGet]
        [Route("api/Items/Search")]
        public IEnumerable<spWS_GetProductsbyCriteriaResult> Search(string criteria, int top = 300)
        {
            var safeTop = top < 1 ? 100 : (top > 1000 ? 1000 : top);
            return db.spWS_GetProductsbyCriteria(criteria).Take(safeTop);
        }

    }
}
