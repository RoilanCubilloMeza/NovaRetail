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
    /// Controlador de órdenes/cotizaciones.
    /// Gestiona la creación y sincronización de órdenes y sus líneas de detalle
    /// en la BD AppCentral.
    /// </summary>
    public class OrdersController : ApiController
    {
        readonly AppCentralDataContext db = new AppCentralDataContext(ConfigurationManager.ConnectionStrings["AppCentralConnectionString"].ConnectionString);

        /// <summary>
        /// Endpoint legado mantenido por compatibilidad.
        /// Actualmente no ejecuta lógica activa y se conserva para no romper clientes antiguos que esperen esta ruta.
        /// </summary>
        [HttpGet]
        public HttpResponseMessage Get()
        {
            return Request.CreateResponse(HttpStatusCode.NotImplemented,
                "Endpoint legado sin implementación activa.");
        }

        /// <summary>
        /// Inserta órdenes y sus líneas de detalle en AppCentral de forma masiva.
        /// El request contiene encabezados y `OrderEntry` asociados, que se procesan juntos para mantener consistencia.
        /// </summary>
        [HttpPost]
        public HttpResponseMessage Post(SyncOrders syncOrders)
        {
            List<Order> order = syncOrders.order;
            List<OrderEntry> orderEntry = syncOrders.orderEntry;
            HttpResponseMessage msg = null;
            string registroActual = "";
            try
            {
                for (int i = 0; i <= order.Count() - 1; i++)
                {
                    db.spAVSCreaOrden(order[i].ID, order[i].StoreID, order[i].Time, order[i].Tax, order[i].Total, order[i].LastUpdated,
                        order[i].ExpirationOrDueDate, order[i].ReferenceNumber, order[i].CustomerFullName, order[i].CustomerAccountNumber, order[i].CorreoEnviado,
                        order[i].SubTotal, order[i].Descuentos, order[i].isProforma, Convert.ToBoolean(order[i].Closed), order[i].CustomerID, order[i].ShipToID,
                        Convert.ToBoolean(order[i].DepositOverride), order[i].Deposit, order[i].SalesRepID, order[i].Type, order[i].Comment, Convert.ToBoolean(order[i].Taxable), order[i].ImpBonificado); //, order[i].ImpBonificado
                    List<OrderEntry> EntriesByOrderID = orderEntry.Where(x => x.OrderID == order[i].ID).ToList();
                    for (int j = 0; j <= EntriesByOrderID.Count() - 1; j++)
                    {
                        db.spAVSCreaOrdenEntry(EntriesByOrderID[j].Cost, order[i].StoreID, EntriesByOrderID[j].ID, EntriesByOrderID[j].OrderID, EntriesByOrderID[j].ItemID, EntriesByOrderID[j].ItemLookupCode, EntriesByOrderID[j].FullPrice,
                            EntriesByOrderID[j].PriceSource, EntriesByOrderID[j].Price, Convert.ToDouble(EntriesByOrderID[j].QuantityOnOrder), EntriesByOrderID[j].SalesRepID, EntriesByOrderID[j].Taxable, EntriesByOrderID[j].DetailID,
                            EntriesByOrderID[j].Description, Convert.ToDouble(EntriesByOrderID[j].QuantityRTD), EntriesByOrderID[j].LastUpdated, EntriesByOrderID[j].Comment, EntriesByOrderID[j].TransactionTime, EntriesByOrderID[j].IsBonificado);//, EntriesByOrderID[j].IsBonificado
                    }
                    msg = Request.CreateResponse(HttpStatusCode.OK, "Registro actualizado");
                }
            }
            catch (Exception ex)
            {
                msg = Request.CreateResponse(HttpStatusCode.InternalServerError, "Error: " + registroActual + " / " + ex.Message.ToString());
            }

            return msg;
        }

    }
}