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
    public class OrdersController : ApiController
    {
        //readonly LINQDataContext db = new LINQDataContext();
        readonly AppCentralDataContext db = new AppCentralDataContext(ConfigurationManager.ConnectionStrings["AppCentralConnectionString"].ConnectionString);

        [HttpGet]
        public HttpResponseMessage Get()
        {
            HttpResponseMessage msg = null;
            /*
            List<Order> order = new List<Order>();
            List< OrderEntry > orderEntry = new List<OrderEntry>();
            HttpResponseMessage msg = null;
            string registroActual = "";
            Order orderitem = new Order();
            orderitem.ID = 1;
            orderitem.CustomerID = 2606;
            orderitem.Tax = 1;
            orderitem.Total = 100;
            orderitem.ReferenceNumber = "55000";
            order.Add(orderitem);
            OrderEntry orderEntryItem = new OrderEntry();
            orderEntryItem.OrderID = 1;
            orderEntryItem.ID = 1;
            orderEntryItem.Price = 10;
            orderEntryItem.QuantityOnOrder = 1;
            orderEntryItem.Description = "Prueba API";
            orderEntryItem.ItemID = 155010;
            orderEntry.Add(orderEntryItem);

            try
            {
                for (int i = 0; i <= order.Count() - 1; i++)
                {
                    db.spAVSCreaOrden(order[i].ID.ToString(), "", order[i].CustomerAccountNumber, order[i].Tax, order[i].Total, order[i].ReferenceNumber,2);
                    //List<OrderEntry> EntriesByOrderID = orderEntry.Where(x => x.OrderID == order[i].ID).ToList();
                    //for (int j = 0; j <= EntriesByOrderID.Count() - 1; j++)
                    //{//La cantidad debería poder ser decimal porque existen cantidades decimales
                    //    db.spAVSCreaOrdenEntry(order[i].ReferenceNumber, EntriesByOrderID[j].ItemID.ToString(), EntriesByOrderID[j].Price, EntriesByOrderID[j].QuantityOnOrder, EntriesByOrderID[j].Description);
                    //}
                    msg = Request.CreateResponse(HttpStatusCode.OK, "Registro actualizado");
                }
            }
            catch (Exception ex)
            {
                msg = Request.CreateResponse(HttpStatusCode.InternalServerError, "Error: " + registroActual + " / " + ex.Message.ToString());
            }
            */
            return msg;
        }

        //Metodo para insertar de forma masiva las ordenes y las orderEntries creados en la BD de SQLLite
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
        /*//Anterior método pos 2021/07/06
        //Metodo para insertar de forma masiva las ordenes y las orderEntries creados en la BD de SQLLite
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
                    db.spAVSCreaOrden(order[i].ID.ToString(), "", order[i].CustomerAccountNumber, order[i].Tax, order[i].Total, order[i].ReferenceNumber, 2);
                    List<OrderEntry> EntriesByOrderID = orderEntry.Where(x => x.OrderID == order[i].ID).ToList();
                    for (int j = 0; j <= EntriesByOrderID.Count() - 1; j++)
                    {//La cantidad debería poder ser decimal porque existen cantidades decimales
                        db.spAVSCreaOrdenEntry(order[i].ReferenceNumber, EntriesByOrderID[j].ItemID.ToString(), EntriesByOrderID[j].Price, EntriesByOrderID[j].QuantityOnOrder, EntriesByOrderID[j].Description);
                    }
                    msg = Request.CreateResponse(HttpStatusCode.OK, "Registro actualizado");
                }
            }
            catch (Exception ex)
            {
                msg = Request.CreateResponse(HttpStatusCode.InternalServerError, "Error: " + registroActual + " / " + ex.Message.ToString());
            }

            return msg;
        }*/

    }
}