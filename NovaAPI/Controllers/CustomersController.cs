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

        private static string Safe(string value, int maxLength)
        {
            var text = (value ?? string.Empty).Trim();
            return text.Length > maxLength ? text.Substring(0, maxLength) : text;
        }

        private void UpsertCustomer(Customer customer)
        {
            var accountNumber = Safe(customer.AccountNumber, 20);
            var firstName = Safe(customer.FirstName, 30);
            var lastName = Safe(customer.LastName, 50);
            var phone1 = Safe(customer.PhoneNumber1, 10);
            var phone2 = Safe(customer.PhoneNumber2, 10);
            var email = Safe(customer.EmailAddress, 255);
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
    FaxNumber = {4},
    EmailAddress = {5},
    CustomText1 = {6},
    CustomText2 = {7},
    CustomText3 = {8},
    CustomText4 = {9},
    CustomText5 = {10},
    Address = {11},
    PriceLevel = {12}
WHERE AccountNumber = {0}",
                    accountNumber, firstName, lastName, phone1, phone2, email,
                    state, city, city2, zip, activityCode, address, priceLevel);
                return;
            }

            db.ExecuteCommand(@"
INSERT INTO Customer
    (AccountNumber, FirstName, LastName, PhoneNumber, FaxNumber, EmailAddress, CustomText1, CustomText2, CustomText3, CustomText4, Address, CustomText5, PriceLevel)
VALUES
    ({0}, {1}, {2}, {3}, {4}, {5}, {6}, {7}, {8}, {9}, {10}, {11}, {12})",
                accountNumber, firstName, lastName, phone1, phone2, email,
                state, city, city2, zip, address, activityCode, priceLevel);
        }

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
    ISNULL(FaxNumber,'')    AS PhoneNumber2,
    ISNULL(EmailAddress,'') AS EmailAddress,
    ISNULL(CustomText1,'')  AS State,
    ISNULL(CustomText2,'')  AS City,
    ISNULL(CustomText3,'')  AS City2,
    ISNULL(CustomText4,'')  AS Zip,
    ISNULL(Address,'')      AS Address,
    ISNULL(CustomText5,'')  AS ActivityCode,
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
    ISNULL(FaxNumber,'')    AS PhoneNumber2,
    ISNULL(EmailAddress,'') AS EmailAddress,
    ISNULL(CustomText1,'')  AS State,
    ISNULL(CustomText2,'')  AS City,
    ISNULL(CustomText3,'')  AS City2,
    ISNULL(CustomText4,'')  AS Zip,
    ISNULL(Address,'')      AS Address,
    ISNULL(CustomText5,'')  AS ActivityCode,
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

        //Metodo para insertar
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
