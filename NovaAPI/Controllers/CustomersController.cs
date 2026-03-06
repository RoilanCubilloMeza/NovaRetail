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
    Address = {10},
    CustomText5 = '',
    PriceLevel = {11}
WHERE AccountNumber = {0}",
                    accountNumber, firstName, lastName, phone1, phone2, email,
                    state, city, city2, zip, address, priceLevel);
                return;
            }

            db.ExecuteCommand(@"
INSERT INTO Customer
    (AccountNumber, FirstName, LastName, PhoneNumber, FaxNumber, EmailAddress, CustomText1, CustomText2, CustomText3, CustomText4, Address, CustomText5, PriceLevel)
VALUES
    ({0}, {1}, {2}, {3}, {4}, {5}, {6}, {7}, {8}, {9}, {10}, '', {11})",
                accountNumber, firstName, lastName, phone1, phone2, email,
                state, city, city2, zip, address, priceLevel);
        }

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
        public IEnumerable<CustomerLookupDto> Get(string criteria)
        {
            try
            {
                var customers = db.spWS_GetCustomersbyCriteria(criteria)
                    .Select(c => new CustomerLookupDto
                    {
                        AccountNumber = c.AccountNumber,
                        FirstName = c.FirstName,
                        LastName = c.LastName,
                        PhoneNumber1 = c.PhoneNumber1,
                        PhoneNumber2 = c.PhoneNumber2,
                        EmailAddress = c.EmailAddress,
                        State = c.STATE,
                        City = c.CITY,
                        City2 = c.CITY2,
                        Zip = c.ZIP,
                        Address = c.Address,
                        CreditDays = c.CreditDays,
                        PriceLevel = c.PriceLevel
                    })
                    .ToList();

                if (customers.Count > 0)
                    return customers;

                var term = (criteria ?? string.Empty).Trim();
                if (string.IsNullOrWhiteSpace(term))
                    return customers;

                return db.spWS_GetCustomers()
                    .Where(c =>
                        string.Equals(c.AccountNumber, term, StringComparison.OrdinalIgnoreCase) ||
                        c.AccountNumber.Contains(term) ||
                        c.FirstName.Contains(term) ||
                        c.LastName.Contains(term) ||
                        (c.FirstName + " " + c.LastName).Contains(term))
                    .Select(c => new CustomerLookupDto
                    {
                        AccountNumber = c.AccountNumber,
                        FirstName = c.FirstName,
                        LastName = c.LastName,
                        PhoneNumber1 = c.PhoneNumber1,
                        PhoneNumber2 = c.PhoneNumber2,
                        EmailAddress = c.EmailAddress,
                        State = c.STATE,
                        City = c.CITY,
                        City2 = c.CITY2,
                        Zip = c.ZIP,
                        Address = c.Address,
                        CreditDays = c.CreditDays,
                        PriceLevel = c.PriceLevel
                    })
                    .ToList();
            }
            catch
            {
                return new List<CustomerLookupDto>();
            }
        }

        //Metodo para insertar de forma masiva todos los clientes creados en la BD de SQLLite y pasarlos al API Rest
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
