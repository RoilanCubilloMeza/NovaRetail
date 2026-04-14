using System;
using System.Collections.Generic;
using System.Configuration;
using System.Data.Common;
using System.Linq;
using System.Net;
using System.Net.Http;
using System.Web.Http;
using NovaAPI.Models;

namespace NovaAPI.Controllers
{
    public class AR_LedgerEntryController : ApiController
    {
        readonly AppCentralDataContext db = new AppCentralDataContext(AppConfig.ConnectionString("AppCentralConnectionString"));

        [HttpPost]
        public HttpResponseMessage Post(SyncLedgerEntry SyncLedgerEntry)
        {
            List<AR_LedgerEntry> LedgerEntries = SyncLedgerEntry.LedgerEntries;
            List<AR_LedgerEntryDetail> LedgerEntryDetails = SyncLedgerEntry.LedgerEntryDetails;
            HttpResponseMessage msg = null;
            string registroActual = "";
            try
            {
                if (db.Connection.State != System.Data.ConnectionState.Open)
                    db.Connection.Open();
                db.Transaction = db.Connection.BeginTransaction();

                for (int i = 0; i <= LedgerEntries.Count() - 1; i++)
                {
                    string ClosedDate = LedgerEntries[i].ClosingDate == null ? "" : LedgerEntries[i].ClosingDate.Value.ToString("yyyy-MM-dd HH:mm:ss");
                    db.spAVSCrea_AR_LedgerEntry(LedgerEntries[i].ID, LedgerEntries[i].LastUpdated, LedgerEntries[i].StoreID, LedgerEntries[i].LinkType, LedgerEntries[i].LinkID, LedgerEntries[i].DocumentType, LedgerEntries[i].PostingDate, LedgerEntries[i].DueDate, LedgerEntries[i].LedgerType, LedgerEntries[i].Description, LedgerEntries[i].CurrencyID, LedgerEntries[i].CurrencyFactor, LedgerEntries[i].Positive, LedgerEntries[i].Open, ClosedDate, LedgerEntries[i].ReasonID, LedgerEntries[i].HoldReasonID, LedgerEntries[i].UndoReasonID, LedgerEntries[i].Comment, LedgerEntries[i].PayMethodID, LedgerEntries[i].TransactionID, LedgerEntries[i].AppReference, LedgerEntries[i].CashierID);
                    List<AR_LedgerEntryDetail> DetailsByLedgerEntryID = LedgerEntryDetails.Where(x => x.LedgerEntryID == LedgerEntries[i].ID).ToList();
                    for (int j = 0; j <= DetailsByLedgerEntryID.Count() - 1; j++)
                    {
                        db.spAVSCrea_AR_LedgerEntryDetail(DetailsByLedgerEntryID[j].ID, DetailsByLedgerEntryID[j].LedgerEntryID, DetailsByLedgerEntryID[j].LedgerType, DetailsByLedgerEntryID[j].DueDate, DetailsByLedgerEntryID[j].PostingDate, DetailsByLedgerEntryID[j].DetailType, DetailsByLedgerEntryID[j].Amount, DetailsByLedgerEntryID[j].AmountLCY, DetailsByLedgerEntryID[j].AmountACY, DetailsByLedgerEntryID[j].AppliedEntryID, DetailsByLedgerEntryID[j].AppliedAmount, DetailsByLedgerEntryID[j].UnapplyEntryID, DetailsByLedgerEntryID[j].UnapplyReasonID, DetailsByLedgerEntryID[j].AppReference, DetailsByLedgerEntryID[j].CashierID, DetailsByLedgerEntryID[j].StoreID);
                    }
                    registroActual = "Registro " + i.ToString();
                }

                db.Transaction.Commit();
                msg = Request.CreateResponse(HttpStatusCode.OK, "Registro actualizado");
            }
            catch (Exception ex)
            {
                try { db.Transaction?.Rollback(); } catch { }
                msg = Request.CreateResponse(HttpStatusCode.InternalServerError, "Error: " + registroActual + " / " + ex.Message.ToString());
            }

            return msg;
        }
    }
}