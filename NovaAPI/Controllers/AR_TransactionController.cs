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
    public class AR_TransactionController : ApiController
    {
        readonly AppCentralDataContext db = new AppCentralDataContext(AppConfig.ConnectionString("AppCentralConnectionString"));

        [HttpPost]
        public HttpResponseMessage Post(List<AR_Transaction> ARTransactions)
        {
            HttpResponseMessage msg = null;
            try
            {
                for (int i = 0; i <= ARTransactions.Count() - 1; i++)
                {
                    db.spAVSCrea_AR_Transaction(ARTransactions[i].ID, ARTransactions[i].UserID, ARTransactions[i].PostingDate, ARTransactions[i].CustomerID, ARTransactions[i].OrderID, ARTransactions[i].DocumentType,
                        ARTransactions[i].Amount, ARTransactions[i].Balance, ARTransactions[i].CashierID, ARTransactions[i].TenderID, ARTransactions[i].ReceivableID,
                        ARTransactions[i].Status, ARTransactions[i].PostedDate, ARTransactions[i].AppReference, ARTransactions[i].StoreID);

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