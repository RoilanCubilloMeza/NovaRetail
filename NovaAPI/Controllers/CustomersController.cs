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
    /// Controlador de clientes. Soporta consulta, búsqueda y upsert (insert/update)
    /// de clientes en la tabla <c>Customer</c> de RMH POS.
    /// </summary>
    public class CustomersController : ApiController
    {
        readonly RMHCDataContext db = new RMHCDataContext(ConfigurationManager.ConnectionStrings["RMHPOS"].ConnectionString);

        /// <summary>
        /// Limpia y recorta textos al largo máximo que admite RMH.
        /// Evita errores de persistencia por cadenas demasiado largas en columnas de cliente.
        /// </summary>
        private static string Safe(string value, int maxLength)
        {
            var text = (value ?? string.Empty).Trim();
            return text.Length > maxLength ? text.Substring(0, maxLength) : text;
        }

        /// <summary>
        /// Inserta o actualiza un cliente según su <c>AccountNumber</c>.
        /// Este método concentra la lógica de normalización previa antes de tocar la tabla <c>Customer</c>.
        /// </summary>
        private void UpsertCustomer(Customer customer)
        {
            var accountNumber = Safe(customer.AccountNumber, 20);
            var firstName = Safe(customer.FirstName, 30);
            var lastName = Safe(customer.LastName, 50);
            var phone1 = Safe(customer.PhoneNumber1, 10);
            var email = Safe(customer.EmailAddress, 255);
            var email2 = Safe(customer.Email2, 255);
            var state = Safe(customer.State, 20);
            var city = Safe(customer.City, 20);
            var city2 = Safe(customer.City2, 20);
            var zip = Safe(customer.Zip, 20);
            var address = Safe(customer.Address, 50);
            var priceLevel = customer.AccountTypeID <= 0 ? 1 : customer.AccountTypeID;
            var activityCode = Safe(customer.ActivityCode, 50);

            var exists = db.ExecuteQuery<int>("SELECT COUNT(1) FROM Customer WHERE AccountNumber = {0}", accountNumber).FirstOrDefault() > 0;

            if (exists)
            {
                db.ExecuteCommand(@"
UPDATE Customer
SET FirstName = {1},
    LastName = {2},
    PhoneNumber = {3},
    EmailAddress = {4},
    CustomText1 = {5},
    CustomText2 = {6},
    State = {7},
    City = {8},
    CustomText3 = {9},
    Zip = {10},
    CustomText5 = {11},
    Address = {12},
    PriceLevel = {13},
    FaxNumber = '',
    CustomText4 = ''
WHERE AccountNumber = {0}",
                    accountNumber, firstName, lastName, phone1, email,
                    accountNumber, email2, state, city, city2, zip, activityCode, address, priceLevel);
                return;
            }

            db.ExecuteCommand(@"
INSERT INTO Customer
    (AccountNumber, FirstName, LastName, PhoneNumber, EmailAddress, CustomText1, CustomText2, State, City, CustomText3, Zip, CustomText5, Address, PriceLevel)
VALUES
    ({0}, {1}, {2}, {3}, {4}, {5}, {6}, {7}, {8}, {9}, {10}, {11}, {12}, {13})",
                accountNumber, firstName, lastName, phone1, email,
                accountNumber, email2, state, city, city2, zip, activityCode, address, priceLevel);
        }

        /// <summary>
        /// Devuelve un listado amplio de clientes para consulta general.
        /// Está pensado para pantallas de búsqueda o sincronización inicial del maestro de clientes.
        /// </summary>
        [HttpGet]
        public IEnumerable<CustomerLookupDto> Get()
        {
            try
            {
                return db.ExecuteQuery<CustomerLookupDto>(@"
SELECT TOP 5000
    AccountNumber,
    FirstName,
    LastName,
    ISNULL(PhoneNumber,'')  AS PhoneNumber1,
    ''                      AS PhoneNumber2,
    ISNULL(EmailAddress,'') AS EmailAddress,
    ISNULL(State,'')        AS State,
    ISNULL(City,'')         AS City,
    ISNULL(CustomText3,'')  AS City2,
    ISNULL(Zip,'')          AS Zip,
    ISNULL(Address,'')      AS Address,
    ISNULL(CustomText5,'')  AS ActivityCode,
    ISNULL(CustomText2,'')  AS Email2,
    CAST(0 AS INT)          AS CreditDays,
    CAST(ISNULL(PriceLevel,1) AS INT) AS PriceLevel
FROM Customer
ORDER BY AccountNumber").ToList();
            }
            catch (Exception ex)
            {
                var msg = ex.InnerException?.Message ?? ex.Message;
                throw new HttpResponseException(
                    Request.CreateResponse(System.Net.HttpStatusCode.InternalServerError,
                        "Get() error: " + msg));
            }
        }

        /// <summary>
        /// Devuelve clientes filtrados por tienda.
        /// Se usa cuando otra pantalla o integración necesita restringir el universo de clientes por sucursal.
        /// </summary>
        [HttpGet]
        [Route("api/Customers/GetByStoreID")]
        public IEnumerable<spWS_GetCustomersByStoreIDResult> Get(int StoreID)
        {
            return db.spWS_GetCustomersByStoreID(StoreID);
        }

        /// <summary>
        /// Devuelve el detalle de cuentas por cobrar asociado a clientes.
        /// Este endpoint sirve como consulta auxiliar para flujos de crédito o AR.
        /// </summary>
        [HttpGet]
        [Route("api/Customers/ARDetail")]
        public IEnumerable<spWS_AR_DetailResult> ARDetail()
        {
            return db.spWS_AR_Detail();
        }

        /// <summary>
        /// Busca clientes por texto libre o coincidencia exacta de cuenta.
        /// Permite consultar por cédula, nombre, apellidos o nombre completo.
        /// </summary>
        [HttpGet]
        public IEnumerable<CustomerLookupDto> Get(string criteria)
        {
            try
            {
                var term = (criteria ?? string.Empty).Trim();

                return db.ExecuteQuery<CustomerLookupDto>(@"
SELECT TOP 200
    AccountNumber,
    FirstName,
    LastName,
    ISNULL(PhoneNumber,'')  AS PhoneNumber1,
    ''                      AS PhoneNumber2,
    ISNULL(EmailAddress,'') AS EmailAddress,
    ISNULL(State,'')        AS State,
    ISNULL(City,'')         AS City,
    ISNULL(CustomText3,'')  AS City2,
    ISNULL(Zip,'')          AS Zip,
    ISNULL(Address,'')      AS Address,
    ISNULL(CustomText5,'')  AS ActivityCode,
    ISNULL(CustomText2,'')  AS Email2,
    CAST(0 AS INT)          AS CreditDays,
    CAST(ISNULL(PriceLevel,1) AS INT) AS PriceLevel
FROM Customer
WHERE AccountNumber = {0}
   OR FirstName     LIKE '%' + {0} + '%'
   OR LastName      LIKE '%' + {0} + '%'
   OR (FirstName + ' ' + LastName) LIKE '%' + {0} + '%'
ORDER BY AccountNumber", term).ToList();
            }
            catch (Exception ex)
            {
                var msg = ex.InnerException?.Message ?? ex.Message;
                throw new HttpResponseException(
                    Request.CreateResponse(System.Net.HttpStatusCode.InternalServerError,
                        "Get(criteria) error: " + msg));
            }
        }

        /// <summary>
        /// Inserta o actualiza una colección de clientes en bloque.
        /// El frontend lo usa como operación de persistencia después de capturar o sincronizar datos del cliente.
        /// </summary>
        [HttpPost]
        public HttpResponseMessage Post([FromBody] List<Customer> cliente)
        {
            if (cliente == null || cliente.Count == 0)
                return Request.CreateResponse(HttpStatusCode.BadRequest, "No se recibió información de cliente.");

            HttpResponseMessage msg = null;
            string registroActual = "";
            try
            {
                for (int i = 0; i < cliente.Count; i++)
                {

                    UpsertCustomer(cliente[i]);

                    registroActual = "Registro " + i.ToString() + " - " + cliente[i].AccountNumber;
                    msg = Request.CreateResponse(HttpStatusCode.OK, "Registro actualizado");
                }
            }
            catch (Exception ex)
            {
                var detail = ex.InnerException != null ? ex.InnerException.Message : ex.Message;
                msg = Request.CreateResponse(HttpStatusCode.InternalServerError, "Error: " + registroActual + " / " + detail);
            }

            return msg;
        }

    }
}
