using System;
using System.Collections.Generic;
using System.Configuration;
using System.Linq;
using System.Net;
using System.Net.Http;
using System.Web.Http;
using NovaAPI.Models;


namespace NovaAPI.Controllers
{
    /// <summary>
    /// Controlador de pagos. Registra pagos de clientes en la BD AppCentral
    /// mediante <c>spAVSCrea_Payment</c>.
    /// </summary>
    public class PaymentController : ApiController
    {
        readonly AppCentralDataContext db = new AppCentralDataContext(ConfigurationManager.ConnectionStrings["AppCentralConnectionString"].ConnectionString);

        /// <summary>
        /// Inserta una colección de pagos en AppCentral.
        /// Se usa para mantener sincronizados los movimientos de cobro asociados a clientes.
        /// </summary>
        [HttpPost]
        public HttpResponseMessage Post(List<Payment> Payments)
        {
            HttpResponseMessage msg = null;
            try
            {
                for (int i = 0; i <= Payments.Count() - 1; i++)
                {
                    db.spAVSCrea_Payment(Payments[i].ID, Payments[i].CashierID, Payments[i].StoreID, Payments[i].CustomerID, Payments[i].Time, Payments[i].Amount, Payments[i].Comment, Payments[i].AppReference);

                    msg = Request.CreateResponse(HttpStatusCode.OK, "Registro actualizado");
                }
            }
            catch (Exception ex)
            {
                msg = Request.CreateResponse(HttpStatusCode.InternalServerError, "Error: " + " / " + ex.Message.ToString());
            }

            return msg;
        }
    }
}