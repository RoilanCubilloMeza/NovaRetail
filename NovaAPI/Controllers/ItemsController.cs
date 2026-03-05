using System;
using System.Collections.Generic;
using System.Configuration;
using System.Web.Http;

namespace NovaAPI.Controllers
{
    public class ItemsController : ApiController
    {
        //readonly LINQDataContext db = new LINQDataContext();
        readonly RMHCDataContext db = new RMHCDataContext(ConfigurationManager.ConnectionStrings["RMHPOS"].ConnectionString);

        [HttpGet]
        public IEnumerable<spWS_GetProductsResult> Get(int storeid, int tipo)
        {
            return db.ExecuteQuery<spWS_GetProductsResult>("EXEC dbo.spWS_GetProducts {0}", storeid);

        }


        [HttpGet]
        public IEnumerable<spWS_GetProductsbyCriteriaResult> Get(string criteria)
        {
            return db.spWS_GetProductsbyCriteria(criteria);
        }

    }
}
