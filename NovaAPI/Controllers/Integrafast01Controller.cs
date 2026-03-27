using System.Collections.Generic;
using System.Configuration;
using System.Linq;
using System.Web.Http;

namespace NovaAPI.Controllers
{
    /// <summary>
    /// Controlador auxiliar de integración con IntegraFast.
    /// Consulta pagos RMH por tienda y representante de ventas.
    /// </summary>
    public class Integrafast01Controller : ApiController
    {
        private readonly RMHCDataContext _db = new RMHCDataContext(ConfigurationManager.ConnectionStrings["RMHPOS"].ConnectionString);

        /// <summary>
        /// Devuelve pagos de RMH filtrados por tienda y vendedor.
        /// Este resultado alimenta procesos o reportes externos ligados a IntegraFast.
        /// </summary>
        [HttpGet]
        public IEnumerable<spAVS_GETPAYMENTS_RMHResult> Get(int storeid, int salesrep_id)
        {
            return _db.spAVS_GETPAYMENTS_RMH(storeid, salesrep_id).ToList();
        }
    }
}