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
    /// Controlador de ubicaciones/sucursales.
    /// Consulta ubicaciones asignadas a un cajero desde la BD AppCentral.
    /// </summary>
    public class LocationController : ApiController
    {
        readonly AppCentralDataContext db = new AppCentralDataContext(ConfigurationManager.ConnectionStrings["AppCentralConnectionString"].ConnectionString);

        /// <summary>
        /// Lista las ubicaciones visibles para un cajero específico.
        /// Se usa para limitar operación o consulta a las sucursales asignadas al usuario.
        /// </summary>
        [HttpGet]
        public IEnumerable<spAVSGetLocationsbyCriteriaResult> Get(int cashierid)
        {
            try
            {
                return db.spAVSGetLocationsbyCriteria(cashierid);
            }
            catch (Exception ex)
            {
                throw ex;
            }

        }

        /// <summary>
        /// Devuelve las rutas asociadas a una referencia específica.
        /// Se usa como complemento del módulo de ubicaciones cuando el flujo trabaja por rutas de visita o reparto.
        /// </summary>
        [HttpGet]
        [Route("api/Location/GetRoutes")]
        public IEnumerable<spAVSGetRoutesbyCriteriaResult> GetRoutes(string referenceNumber)
        {
            try
            {
                return db.spAVSGetRoutesbyCriteria(referenceNumber);
            }
            catch (Exception ex)
            {
                throw ex;
            }

        }

        /// <summary>
        /// Sincroniza rutas y ubicaciones relacionadas en AppCentral.
        /// Primero registra las rutas y luego persiste cada ubicación asociada a la referencia de ruta.
        /// </summary>
        [HttpPost]
        public HttpResponseMessage Post(SyncLocation syncLocations)
        {
            List<Rutas> routes = syncLocations.Routes;
            List<Ubicaciones> locations = syncLocations.Locations;
            HttpResponseMessage msg = null;
            string registroActual = "";
            try
            {
                for (int i = 0; i <= routes.Count() - 1; i++)
                {
                    db.spAVSCreaRoute(routes[i].StoreID, routes[i].ReferenceNumber, routes[i].Nombre, routes[i].CashierID);
                    List<Ubicaciones> LocationsByRouteID = locations.Where(x => x.RutaID == routes[i].ReferenceNumber).ToList();
                }
                for (int j = 0; j <= locations.Count() - 1; j++)
                {
                    db.spAVSCreaLocation(locations[j].RutaID, locations[j].CustomerID, locations[j].Nombre, locations[j].Descripcion, locations[j].Latitud, locations[j].Longitud, locations[j].Tipo);
                }
                msg = Request.CreateResponse(HttpStatusCode.OK, "Registro actualizado");

            }
            catch (Exception ex)
            {
                msg = Request.CreateResponse(HttpStatusCode.InternalServerError, "Error: " + registroActual + " / " + ex.Message.ToString());
            }

            return msg;
        }
    }
}