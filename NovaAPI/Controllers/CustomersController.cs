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


    public class CustomersController : ApiController
    {
        //string cs = ConfigurationManager.ConnectionStrings["RMHPOS"].ConnectionString;
        //readonly LINQDataContext db = new LINQDataContext();
        readonly RMHCDataContext db = new RMHCDataContext(ConfigurationManager.ConnectionStrings["RMHPOS"].ConnectionString);//test

        [HttpGet]
        public IEnumerable<spWS_GetCustomersResult> Get()
        {
            return db.spWS_GetCustomers();
        }

        [HttpGet]
        [Route("api/Customers/GetByStoreID")]
        public IEnumerable<spWS_GetCustomersByStoreIDResult> Get(int StoreID)
        {
            return db.spWS_GetCustomersByStoreID(StoreID);
        }

        [HttpGet]
        [Route("api/Customers/ARDetail")]
        public IEnumerable<spWS_AR_DetailResult> ARDetail()
        {
            return db.spWS_AR_Detail();
        }

        [HttpGet]
        public IEnumerable<spWS_GetCustomersbyCriteriaResult> Get(string criteria)
        {
            return db.spWS_GetCustomersbyCriteria(criteria);
        }

        //Metodo para insertar de forma masiva todos los clientes creados en la BD de SQLLite y pasarlos al API Rest
        public HttpResponseMessage Post(List<Customer> cliente)
        {
            HttpResponseMessage msg = null;
            string registroActual = "";
            try
            {
                for (int i = 0; i <= cliente.Count() - 1; i++)
                {

                    db.spWS_InsertActualizaCustomers(cliente[i].AccountNumber, cliente[i].FirstName, cliente[i].LastName,
                    cliente[i].PhoneNumber1, cliente[i].PhoneNumber2, cliente[i].EmailAddress, "", "", "", "", cliente[i].Address, ""
                    , "WC_API", cliente[i].AccountTypeID.ToString(), cliente[i].Vendedor, cliente[i].CreditDays);

                    registroActual = "Registro " + i.ToString() + " - " + cliente[i].AccountNumber;
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
