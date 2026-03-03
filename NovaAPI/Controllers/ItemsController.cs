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
            try
            {
                return db.spWS_GetProducts(storeid, tipo);
            }
            catch (Exception ex)
            {
                throw ex;
            }

        }


        [HttpGet]
        public IEnumerable<spWS_GetProductsbyCriteriaResult> Get(string criteria)
        {
            return db.spWS_GetProductsbyCriteria(criteria);
        }

    }
}
