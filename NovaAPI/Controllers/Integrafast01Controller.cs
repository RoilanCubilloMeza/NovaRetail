using System;
using System.Collections.Generic;
using System.Configuration;
using System.Linq;
using System.Web.Http;

namespace NovaAPI.Controllers
{
    public class Integrafast01Controller : ApiController
    {
        readonly RMHCDataContext db = new RMHCDataContext(ConfigurationManager.ConnectionStrings["RMHPOS"].ConnectionString);

        [HttpGet]
        public IEnumerable<spAVS_GETPAYMENTS_RMHResult> Get(int storeid, int salesrep_id)
        {
            try
            {
                var result = db.spAVS_GETPAYMENTS_RMH(storeid, salesrep_id).ToList();
                return result;
            }
            catch (Exception ex)
            {
                throw ex;
            }
        }
    }
}