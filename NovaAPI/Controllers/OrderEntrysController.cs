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
    public class OrderEntrysController : ApiController
    {
        readonly AppCentralDataContext db = new AppCentralDataContext(AppConfig.ConnectionString("RMHPOS"));
        /*
        [HttpGet]
        public IEnumerable<spWS_GetCustomersResult> Get()
        {
            return db.spWS_GetCustomers();
        }
        */

        //Metodo para insertar de forma masiva todos los clientes creados en la BD de SQLLite y pasarlos al API Rest
        public HttpResponseMessage Post(List<OrderEntry> orderEntry)
        {
            HttpResponseMessage msg = null;
            string registroActual = "";
            try
            {
                for (int i = 0; i <= orderEntry.Count() - 1; i++)
                {//La cantidad debería poder ser decimal porque existen cantidades decimales
                    //db.spAVSCreaOrdenEntry(orderEntry[i].OrderID.ToString(), orderEntry[i].ItemID.ToString(),orderEntry[i].Price,int.Parse(orderEntry[i].QuantityOnOrder.ToString()),orderEntry[i].Description);
                    //registroActual = "Registro " + i.ToString() + " - " + orderEntry[i].ID;
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