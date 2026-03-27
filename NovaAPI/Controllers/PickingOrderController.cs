using System;
using System.Collections.Generic;
using System.Configuration;
using System.Net;
using System.Net.Http;
using System.Web.Http;
using NovaAPI.Models;

namespace NovaAPI.Controllers
{
    /// <summary>
    /// Controlador de órdenes de picking (preparación de pedidos).
    /// Consulta encabezados y detalles de órdenes de picking desde la BD RMH POS.
    /// </summary>
    public class PickingOrderController : ApiController
    {
        private readonly RMHCDataContext _db = new RMHCDataContext(ConfigurationManager.ConnectionStrings["RMHPOS"].ConnectionString);
        private readonly AppCentralDataContext _dbApp = new AppCentralDataContext(ConfigurationManager.ConnectionStrings["AppCentralConnectionString"].ConnectionString);

        /// <summary>
        /// Obtiene encabezados de órdenes de picking.
        /// Normalmente lo consume una interfaz de preparación de pedidos o despacho.
        /// </summary>
        [HttpGet]
        [Route("api/PickingOrder/GetOrders")]
        public IEnumerable<spWS_GetPOD_HeaderResult> GetOrders()
        {
            return _db.spWS_GetPOD_Header();
        }

        /// <summary>
        /// Obtiene el detalle de líneas de picking desde RMH.
        /// Complementa el encabezado para construir una vista completa de preparación de pedidos.
        /// </summary>
        [HttpGet]
        [Route("api/PickingOrder/GetDetails")]
        public IEnumerable<spWS_GetPOD_DetailResult> GetDetail()
        {
            return _db.spWS_GetPOD_Detail();
        }

        /// <summary>
        /// Sincroniza encabezados de órdenes de picking hacia AppCentral.
        /// Se usa cuando otra aplicación o dispositivo genera o actualiza pedidos de preparación.
        /// </summary>
        [HttpPost]
        [Route("api/PickingOrder/PostOrders")]
        public HttpResponseMessage PostOrder(List<PickingOrder> pickingOrder)
        {
            if (pickingOrder == null || pickingOrder.Count == 0)
                return Request.CreateResponse(HttpStatusCode.BadRequest, "No se recibieron órdenes de picking para sincronizar.");

            try
            {
                foreach (var order in pickingOrder)
                {
                    _dbApp.spAVS_CreaPODOrder(order.ID, order.StoreID, order.RmsID, order.LastUpdated, order.Number, order.Status,
                        order.SupplierID, order.DateCreated, order.OrderDate, order.RequiredDate,
                        order.DatePlaced, order.LocationType, order.LocationID, order.Reference, order.AddrTo,
                        order.ShipTo, order.PurchaserID, order.ShipViaID, order.PayTermID, order.ExchangeRate,
                        order.Comment, order.TotalAmount, order.TotalTax, order.SupplierName, order.PhoneNumber.ToString(), order.User);
                }

                return Request.CreateResponse(HttpStatusCode.OK, "Órdenes de picking sincronizadas correctamente.");
            }
            catch (Exception ex)
            {
                return Request.CreateResponse(HttpStatusCode.InternalServerError, "Error al sincronizar órdenes de picking: " + ex.Message);
            }
        }

        /// <summary>
        /// Sincroniza las líneas de una orden de picking hacia AppCentral.
        /// Debe ejecutarse junto con el encabezado para completar la orden preparada.
        /// </summary>
        [HttpPost]
        [Route("api/PickingOrder/PostOrderEntries")]
        public HttpResponseMessage PostOneOrder(List<PickingOrderEntry> pickingOrderEntries)
        {
            if (pickingOrderEntries == null || pickingOrderEntries.Count == 0)
                return Request.CreateResponse(HttpStatusCode.BadRequest, "No se recibieron líneas de picking para sincronizar.");

            try
            {
                foreach (var entry in pickingOrderEntries)
                {
                    _dbApp.spAVSCrea_PODOrderEntry(entry.ID, entry.StoreID, entry.LastUpdated, entry.OrderID, entry.LineType,
                        entry.LineNumber, entry.EntryType, entry.EntryID, entry.ItemTaxID, entry.OrderNumber,
                        entry.Description, entry.UOMID, (double?)entry.Quantity, (double?)entry.QtyReceived, entry.UnitCost,
                        entry.Comment);
                }

                return Request.CreateResponse(HttpStatusCode.OK, "Líneas de picking sincronizadas correctamente.");
            }
            catch (Exception ex)
            {
                return Request.CreateResponse(HttpStatusCode.InternalServerError, "Error al sincronizar líneas de picking: " + ex.Message);
            }
        }
    }
}