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
    /// Controlador de entradas de formas de pago (TenderEntry).
    /// Registra los detalles de pago de cada transacción en la BD AppCentral.
    /// </summary>
    public class TenderEntryController : ApiController
    {
        readonly AppCentralDataContext db = new AppCentralDataContext(ConfigurationManager.ConnectionStrings["AppCentralConnectionString"].ConnectionString);

        [HttpPost]
        public HttpResponseMessage Post(List<TenderEntry> TenderEntries)
        {
            HttpResponseMessage msg = null;
            try
            {
                for (int i = 0; i <= TenderEntries.Count() - 1; i++)
                {
                    db.spAVSCrea_TenderEntry(TenderEntries[i].ID, TenderEntries[i].CreditCardExpiration, TenderEntries[i].OrderHistoryID, TenderEntries[i].DropPayoutID, TenderEntries[i].StoreID, TenderEntries[i].TransactionNumber, TenderEntries[i].TenderCode, TenderEntries[i].PaymentID, TenderEntries[i].Description, TenderEntries[i].CreditCardNumber, TenderEntries[i].CreditCardApprovalCode, TenderEntries[i].Amount, TenderEntries[i].AccountHolder, TenderEntries[i].RoundingError, TenderEntries[i].AmountForeign, TenderEntries[i].BankNumber, TenderEntries[i].SerialNumber, TenderEntries[i].State, TenderEntries[i].License, TenderEntries[i].TransitNumber, TenderEntries[i].VisaNetAuthorizationID, TenderEntries[i].DebitSurcharge, TenderEntries[i].CashBackSurcharge, TenderEntries[i].IsCreateNew, TenderEntries[i].AppReference, TenderEntries[i].CashierID);

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