using System.Collections.Generic;
using System.Linq;
using System.Web.Http;

namespace NovaAPI.Controllers
{
    public class Integrafast01Controller : ApiController
    {
        private readonly RMHCDataContext db = new RMHCDataContext(AppConfig.ConnectionString("RMHPOS"));

        [HttpGet]
        public IEnumerable<spAVS_GETPAYMENTS_RMHResult> Get(int storeid, int salesrep_id)
        {
            return db.spAVS_GETPAYMENTS_RMH(storeid, salesrep_id).ToList();
        }
    }
}
