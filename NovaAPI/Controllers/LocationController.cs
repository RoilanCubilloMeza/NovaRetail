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
    public class LocationController : ApiController
    {
        readonly AppCentralDataContext db = new AppCentralDataContext(AppConfig.ConnectionString("AppCentralConnectionString"));

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

        [HttpPost]
        public HttpResponseMessage Post(SyncLocation syncLocations)
        {
            List<Rutas> routes = syncLocations.Routes;
            List<Ubicaciones> locations = syncLocations.Locations;
            HttpResponseMessage msg = null;
            string registroActual = "";
            try
            {
                if (db.Connection.State != System.Data.ConnectionState.Open)
                    db.Connection.Open();
                db.Transaction = db.Connection.BeginTransaction();

                for (int i = 0; i <= routes.Count() - 1; i++)
                {
                    db.spAVSCreaRoute(routes[i].StoreID, routes[i].ReferenceNumber, routes[i].Nombre, routes[i].CashierID);
                    List<Ubicaciones> LocationsByRouteID = locations.Where(x => x.RutaID == routes[i].ReferenceNumber).ToList();
                }
                for (int j = 0; j <= locations.Count() - 1; j++)
                {
                    db.spAVSCreaLocation(locations[j].RutaID, locations[j].CustomerID, locations[j].Nombre, locations[j].Descripcion, locations[j].Latitud, locations[j].Longitud, locations[j].Tipo);
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