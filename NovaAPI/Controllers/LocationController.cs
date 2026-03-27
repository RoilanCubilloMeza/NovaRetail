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
        private readonly AppCentralDataContext _db = new AppCentralDataContext(ConfigurationManager.ConnectionStrings["AppCentralConnectionString"].ConnectionString);

        /// <summary>
        /// Lista las ubicaciones visibles para un cajero específico.
        /// Se usa para limitar operación o consulta a las sucursales asignadas al usuario.
        /// </summary>
        [HttpGet]
        public IEnumerable<spAVSGetLocationsbyCriteriaResult> Get(int cashierid)
        {
            return _db.spAVSGetLocationsbyCriteria(cashierid);
        }

        /// <summary>
        /// Devuelve las rutas asociadas a una referencia específica.
        /// Se usa como complemento del módulo de ubicaciones cuando el flujo trabaja por rutas de visita o reparto.
        /// </summary>
        [HttpGet]
        [Route("api/Location/GetRoutes")]
        public IEnumerable<spAVSGetRoutesbyCriteriaResult> GetRoutes(string referenceNumber)
        {
            return _db.spAVSGetRoutesbyCriteria(referenceNumber);
        }

        /// <summary>
        /// Sincroniza rutas y ubicaciones relacionadas en AppCentral.
        /// Primero registra las rutas y luego persiste cada ubicación asociada a la referencia de ruta.
        /// </summary>
        [HttpPost]
        public HttpResponseMessage Post(SyncLocation syncLocations)
        {
            if (syncLocations == null)
                return Request.CreateResponse(HttpStatusCode.BadRequest, "No se recibió información de rutas y ubicaciones.");

            var routes = syncLocations.Routes ?? new List<Rutas>();
            var locations = syncLocations.Locations ?? new List<Ubicaciones>();

            try
            {
                foreach (var route in routes)
                {
                    _db.spAVSCreaRoute(route.StoreID, route.ReferenceNumber, route.Nombre, route.CashierID);
                }

                foreach (var location in locations)
                {
                    _db.spAVSCreaLocation(location.RutaID, location.CustomerID, location.Nombre, location.Descripcion, location.Latitud, location.Longitud, location.Tipo);
                }

                return Request.CreateResponse(HttpStatusCode.OK, "Rutas y ubicaciones sincronizadas correctamente.");
            }
            catch (Exception ex)
            {
                return Request.CreateResponse(HttpStatusCode.InternalServerError, "Error al sincronizar rutas y ubicaciones: " + ex.Message);
            }
        }
    }
}