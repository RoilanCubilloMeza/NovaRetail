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
    public class TransactionController : ApiController
    {
        readonly AppCentralDataContext db = new AppCentralDataContext(AppConfig.ConnectionString("AppCentralConnectionString"));
        [HttpPost]
        public HttpResponseMessage Post(SyncTransaction syncTransactions)
        {
            List<Transaction> transactions = syncTransactions.transactions;
            List<TransactionEntry> TransactionEntry = syncTransactions.transactionEntries;
            HttpResponseMessage msg = null;
            string registroActual = "";
            try
            {
                if (db.Connection.State != System.Data.ConnectionState.Open)
                    db.Connection.Open();
                db.Transaction = db.Connection.BeginTransaction();

                for (int i = 0; i <= transactions.Count() - 1; i++)
                {
                    db.spAVSCreaTransaction(transactions[i].ShipToID, transactions[i].StoreID, transactions[i].TransactionNumber, transactions[i].BatchNumber, transactions[i].Time,
                        transactions[i].CustomerID.ToString(), transactions[i].CashierID, Convert.ToDecimal(transactions[i].Total), Convert.ToDecimal(transactions[i].SalesTax), transactions[i].Comment, transactions[i].ReferenceNumber,
                        transactions[i].Status, transactions[i].ChannelType, transactions[i].RecallID, transactions[i].RecallType, transactions[i].ExchangeID,
                        transactions[i].CustomerFullName, transactions[i].CustomerAccountNumber, transactions[i].SubTotal, transactions[i].Descuentos, transactions[i].SalesRepID);
                    List<TransactionEntry> EntriesByTransactionID = TransactionEntry.Where(x => x.TransactionNumber == transactions[i].TransactionNumber).ToList();
                    for (int j = 0; j <= EntriesByTransactionID.Count() - 1; j++)
                    {
                        db.spAVSCreaTransactionEntry(EntriesByTransactionID[j].AutoID, Convert.ToDecimal(EntriesByTransactionID[j].Commission), Convert.ToDecimal(EntriesByTransactionID[j].Cost), Convert.ToDecimal(EntriesByTransactionID[j].FullPrice), EntriesByTransactionID[j].StoreID,
                            EntriesByTransactionID[j].TransactionNumber, EntriesByTransactionID[j].ItemID, Convert.ToDecimal(EntriesByTransactionID[j].Price), Convert.ToDecimal(EntriesByTransactionID[j].PriceSource), EntriesByTransactionID[j].Quantity, EntriesByTransactionID[j].SalesRepID, Convert.ToBoolean(EntriesByTransactionID[j].Taxable),
                            EntriesByTransactionID[j].DetailID, EntriesByTransactionID[j].Comment, EntriesByTransactionID[j].DiscountReasonCodeID, EntriesByTransactionID[j].ReturnReasonCodeID, EntriesByTransactionID[j].TaxChangeReasonCodeID,
                            Convert.ToDecimal(EntriesByTransactionID[j].SalesTax), EntriesByTransactionID[j].QuantityDiscountID, EntriesByTransactionID[j].ItemType, EntriesByTransactionID[j].ComputedQuantity, EntriesByTransactionID[j].TransactionTime,
                            Convert.ToBoolean(EntriesByTransactionID[j].IsAddMoney), EntriesByTransactionID[j].VoucherID, EntriesByTransactionID[j].PrecioEditado);
                    }
                    registroActual = "Registro " + i.ToString();
                }

                db.Transaction.Commit();
                msg = Request.CreateResponse(HttpStatusCode.OK, "Registro actualizado");
            }
            catch (Exception ex)
            {
                try { db.Transaction?.Rollback(); } catch { }
                msg = Request.CreateResponse(HttpStatusCode.InternalServerError, "Error interno al sincronizar transacciones.");
            }

            return msg;
        }
    }
}
