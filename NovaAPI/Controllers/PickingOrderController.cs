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
    public class PickingOrderController : ApiController
    {
        readonly RMHCDataContext db = new RMHCDataContext(ConfigurationManager.ConnectionStrings["RMHPOS"].ConnectionString);
        readonly AppCentralDataContext dbApp = new AppCentralDataContext(ConfigurationManager.ConnectionStrings["AppCentralConnectionString"].ConnectionString);

        [HttpGet]
        [Route("api/PickingOrder/GetOrders")]
        public IEnumerable<spWS_GetPOD_HeaderResult> GetOrders()
        {
            try
            {
                return db.spWS_GetPOD_Header();
            }
            catch (Exception ex)
            {
                throw ex;
            }
        }

        [HttpGet]
        [Route("api/PickingOrder/GetDetails")]
        public IEnumerable<spWS_GetPOD_DetailResult> GetDetail()
        {
            try
            {
                return db.spWS_GetPOD_Detail();
            }
            catch (Exception ex)
            {
                throw ex;
            }
        }

        [HttpPost]
        [Route("api/PickingOrder/PostOrders")]
        public HttpResponseMessage PostOrder(List<PickingOrder> pickingOrder)
        {
            HttpResponseMessage msg = null;
            try
            {
                for (int i = 0; i <= pickingOrder.Count() - 1; i++)
                {
                    dbApp.spAVS_CreaPODOrder(pickingOrder[i].ID, pickingOrder[i].StoreID, pickingOrder[i].RmsID, pickingOrder[i].LastUpdated, pickingOrder[i].Number, pickingOrder[i].Status,
                                            pickingOrder[i].SupplierID, pickingOrder[i].DateCreated, pickingOrder[i].OrderDate, pickingOrder[i].RequiredDate,
                                            pickingOrder[i].DatePlaced, pickingOrder[i].LocationType, pickingOrder[i].LocationID, pickingOrder[i].Reference, pickingOrder[i].AddrTo,
                                            pickingOrder[i].ShipTo, pickingOrder[i].PurchaserID, pickingOrder[i].ShipViaID, pickingOrder[i].PayTermID, pickingOrder[i].ExchangeRate,
                                            pickingOrder[i].Comment, pickingOrder[i].TotalAmount, pickingOrder[i].TotalTax, pickingOrder[i].SupplierName, pickingOrder[i].PhoneNumber.ToString(), pickingOrder[i].User);

                    msg = Request.CreateResponse(HttpStatusCode.OK, "Registro sincronizado");
                }
            }
            catch (Exception ex)
            {
                msg = Request.CreateResponse(HttpStatusCode.InternalServerError, "Error: " + " / " + ex.Message.ToString());
            }

            return msg;
        }

        [HttpPost]
        [Route("api/PickingOrder/PostOrderEntries")]
        public HttpResponseMessage PostOneOrder(List<PickingOrderEntry> pickingOrderEntries)
        {
            HttpResponseMessage msg = null;
            try
            {
                for (int i = 0; i <= pickingOrderEntries.Count() - 1; i++)
                {
                    dbApp.spAVSCrea_PODOrderEntry(pickingOrderEntries[i].ID, pickingOrderEntries[i].StoreID, pickingOrderEntries[i].LastUpdated, pickingOrderEntries[i].OrderID, pickingOrderEntries[i].LineType,
                                                  pickingOrderEntries[i].LineNumber, pickingOrderEntries[i].EntryType, pickingOrderEntries[i].EntryID, pickingOrderEntries[i].ItemTaxID, pickingOrderEntries[i].OrderNumber,
                                                  pickingOrderEntries[i].Description, pickingOrderEntries[i].UOMID, (double?)pickingOrderEntries[i].Quantity, (double?)pickingOrderEntries[i].QtyReceived, pickingOrderEntries[i].UnitCost,
                                                  pickingOrderEntries[i].Comment);
                }
                msg = Request.CreateResponse(HttpStatusCode.OK, "Registro actualizado");

            }
            catch (Exception ex)
            {
                msg = Request.CreateResponse(HttpStatusCode.InternalServerError, "Error: " + " / " + ex.Message.ToString());
            }

            return msg;
        }
    }
}