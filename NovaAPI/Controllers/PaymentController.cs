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
    public class PaymentController : ApiController
    {
        //dbAppCentral = AppConfig.ConnectionString("AppCentralConnectionString");
        readonly AppCentralDataContext db = new AppCentralDataContext(AppConfig.ConnectionString("AppCentralConnectionString"));

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
                msg = Request.CreateResponse(HttpStatusCode.InternalServerError, "Error interno al registrar pagos.");
            }

            return msg;
        }
    }
}