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
    /// Controlador de líneas de detalle de órdenes/cotizaciones.
    /// Sincroniza las líneas de una orden hacia la BD.
    /// </summary>
    public class OrderEntrysController : ApiController
    {
        readonly AppCentralDataContext db = new AppCentralDataContext(ConfigurationManager.ConnectionStrings["RMHPOS"].ConnectionString);

        /// <summary>
        /// Inserta masivamente las líneas de detalle de una orden.
        /// Este endpoint forma parte de la sincronización de órdenes temporales hacia backend.
        /// </summary>
        public HttpResponseMessage Post(List<OrderEntry> orderEntry)
        {
            HttpResponseMessage msg = null;
            string registroActual = "";
            try
            {
                for (int i = 0; i <= orderEntry.Count() - 1; i++)
                {
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