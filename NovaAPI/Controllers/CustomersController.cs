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

        private const string BaseSelectSql = @"
SELECT AccountNumber, FirstName, LastName,
       PhoneNumber  AS PhoneNumber1,
       FaxNumber    AS PhoneNumber2,
       EmailAddress,
       State, City,
       CustomText3  AS City2,
       Zip, Address,
       CustomText5  AS ActivityCode,
       ISNULL(AccountTypeID, 0) AS AccountTypeID,
       CAST(PriceLevel AS INT) AS PriceLevel
FROM Customer";

        [HttpGet]
        public IEnumerable<CustomerLookupDto> Get()
        {
            try
            {
                return db.ExecuteQuery<CustomerLookupDto>(BaseSelectSql).ToList();
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
        [Route("api/Customers/CreditInfo")]
        public HttpResponseMessage CreditInfo(string accountNumber)
        {
            try
            {
                var term = (accountNumber ?? string.Empty).Trim();
                if (string.IsNullOrEmpty(term))
                    return Request.CreateResponse(HttpStatusCode.BadRequest, "accountNumber es requerido.");

                var results = db.spWS_GetCustomers();
                var match = results?.FirstOrDefault(c =>
                    string.Equals((c.AccountNumber ?? string.Empty).Trim(), term, StringComparison.OrdinalIgnoreCase));

                if (match == null)
                {
                    return Request.CreateResponse(HttpStatusCode.OK, new CustomerCreditInfoDto
                    {
                        AccountNumber = term,
                        HasCredit = false
                    });
                }

                var creditLimit = match.CreditLimit ?? 0m;
                var hasCredit = creditLimit > 0;

                return Request.CreateResponse(HttpStatusCode.OK, new CustomerCreditInfoDto
                {
                    ID = match.ID,
                    AccountNumber = match.AccountNumber ?? term,
                    FirstName = match.FirstName ?? string.Empty,
                    LastName = match.LastName ?? string.Empty,
                    AccountTypeID = match.PriceLevel,
                    CreditDays = match.CreditDays,
                    ClosingBalance = match.ClosingBalance ?? 0m,
                    CreditLimit = creditLimit,
                    Available = match.Available ?? 0m,
                    HasCredit = hasCredit
                });
            }
            catch (Exception ex)
            {
                var msg = ex.InnerException?.Message ?? ex.Message;
                return Request.CreateResponse(HttpStatusCode.InternalServerError, "CreditInfo error: " + msg);
            }
        }

        [HttpGet]
        public IEnumerable<CustomerLookupDto> Get(string criteria)
        {
            try
            {
                var term = (criteria ?? string.Empty).Trim();
                if (string.IsNullOrEmpty(term))
                    return Get();

                var like = "%" + term + "%";
                var sql = BaseSelectSql + @"
WHERE AccountNumber LIKE {0}
   OR FirstName     LIKE {0}
   OR LastName      LIKE {0}
   OR PhoneNumber   LIKE {0}
   OR FaxNumber     LIKE {0}
   OR EmailAddress  LIKE {0}
   OR Address       LIKE {0}
   OR City          LIKE {0}
   OR State         LIKE {0}
   OR Zip           LIKE {0}";

                return db.ExecuteQuery<CustomerLookupDto>(sql, like).ToList();
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
