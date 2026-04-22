using System;
using System.Collections.Generic;
using System.Configuration;
using System.Data;
using System.Data.SqlClient;
using System.Linq;
using System.Net;
using System.Net.Http;
using System.Web.Http;
using NovaAPI.Models;

namespace NovaAPI.Controllers
{


    public class CustomersController : ApiController
    {
        private const decimal LedgerClosingTolerance = 0.01m;

        //string cs = AppConfig.ConnectionString("RMHPOS");
        //readonly LINQDataContext db = new LINQDataContext();
        readonly RMHCDataContext db = new RMHCDataContext(AppConfig.ConnectionString("RMHPOS"));//test

        private static string Safe(string value, int maxLength)
        {
            var text = (value ?? string.Empty).Trim();
            return text.Length > maxLength ? text.Substring(0, maxLength) : text;
        }

        private sealed class LedgerBalanceSnapshot
        {
            public decimal BaseAmount { get; set; }
            public decimal AppliedToEntry { get; set; }
            public decimal AppliedByEntry { get; set; }
        }

        private static LedgerBalanceSnapshot LoadLedgerBalanceSnapshot(SqlConnection cn, SqlTransaction tx, int ledgerEntryID)
        {
            using (var cmd = new SqlCommand(@"
SELECT
    BaseAmount = ISNULL(
        (
            SELECT SUM(d.Amount)
            FROM dbo.AR_LedgerEntryDetail d
            WHERE d.LedgerEntryID = @LedgerEntryID
              AND d.AppliedEntryID = 0
        ), 0),
    AppliedToEntry = ISNULL(
        (
            SELECT SUM(d.AppliedAmount)
            FROM dbo.AR_LedgerEntryDetail d
            WHERE d.AppliedEntryID = @LedgerEntryID
        ), 0),
    AppliedByEntry = ISNULL(
        (
            SELECT SUM(ABS(d.AppliedAmount))
            FROM dbo.AR_LedgerEntryDetail d
            WHERE d.LedgerEntryID = @LedgerEntryID
              AND d.AppliedEntryID > 0
        ), 0);", cn, tx))
            {
                cmd.Parameters.AddWithValue("@LedgerEntryID", ledgerEntryID);

                using (var reader = cmd.ExecuteReader())
                {
                    if (!reader.Read())
                        return new LedgerBalanceSnapshot();

                    return new LedgerBalanceSnapshot
                    {
                        BaseAmount = reader["BaseAmount"] == DBNull.Value ? 0m : Convert.ToDecimal(reader["BaseAmount"]),
                        AppliedToEntry = reader["AppliedToEntry"] == DBNull.Value ? 0m : Convert.ToDecimal(reader["AppliedToEntry"]),
                        AppliedByEntry = reader["AppliedByEntry"] == DBNull.Value ? 0m : Convert.ToDecimal(reader["AppliedByEntry"])
                    };
                }
            }
        }

        private static void SetLedgerEntryOpenState(SqlConnection cn, SqlTransaction tx, int ledgerEntryID, bool isOpen, DateTime now)
        {
            using (var cmd = new SqlCommand(@"
UPDATE dbo.AR_LedgerEntry
SET ClosingDate = CASE WHEN @Open = 1 THEN NULL ELSE ISNULL(ClosingDate, @Now) END,
    LastUpdated = @Now
WHERE ID = @LedgerEntryID;", cn, tx))
            {
                cmd.Parameters.AddWithValue("@LedgerEntryID", ledgerEntryID);
                cmd.Parameters.AddWithValue("@Open", isOpen ? 1 : 0);
                cmd.Parameters.AddWithValue("@Now", now);
                cmd.ExecuteNonQuery();
            }
        }

        private static void RefreshLedgerApplicationStatuses(SqlConnection cn, SqlTransaction tx, int sourceLedgerEntryID, IEnumerable<int> targetLedgerEntryIDs, DateTime now)
        {
            if (sourceLedgerEntryID > 0)
            {
                var sourceSnapshot = LoadLedgerBalanceSnapshot(cn, tx, sourceLedgerEntryID);
                var sourceRemaining = Math.Max(0m, Math.Abs(sourceSnapshot.BaseAmount) - sourceSnapshot.AppliedByEntry);
                SetLedgerEntryOpenState(cn, tx, sourceLedgerEntryID, sourceRemaining > LedgerClosingTolerance, now);
            }

            foreach (var targetLedgerEntryID in (targetLedgerEntryIDs ?? Enumerable.Empty<int>()).Distinct().Where(id => id > 0))
            {
                var targetSnapshot = LoadLedgerBalanceSnapshot(cn, tx, targetLedgerEntryID);
                var targetBalance = Math.Max(0m, targetSnapshot.BaseAmount + targetSnapshot.AppliedToEntry);
                SetLedgerEntryOpenState(cn, tx, targetLedgerEntryID, targetBalance > LedgerClosingTolerance, now);
            }
        }

        private static (bool Ok, string Message) TryAppCentralPayment(
            string appCentralCs,
            int cashierId,
            int storeId,
            string accountNumber,
            DateTime now,
            decimal amount,
            string comment,
            string reference)
        {
            if (string.IsNullOrWhiteSpace(appCentralCs))
                return (false, "Cadena de conexión AppCentral no configurada.");

            var timer = System.Diagnostics.Stopwatch.StartNew();
            try
            {
                using (var cn = new SqlConnection(appCentralCs))
                {
                    cn.Open();
                    using (var cmd = new SqlCommand("dbo.spAVSCrea_Payment", cn))
                    {
                        cmd.CommandType = CommandType.StoredProcedure;
                        cmd.CommandTimeout = 10;
                        cmd.Parameters.AddWithValue("@ID", 0);
                        cmd.Parameters.AddWithValue("@CashierID", cashierId);
                        cmd.Parameters.AddWithValue("@StoreID", storeId);
                        cmd.Parameters.AddWithValue("@CustomerAccountNumber", accountNumber);
                        cmd.Parameters.AddWithValue("@Time", now.ToString("yyyy-MM-dd HH:mm:ss"));
                        cmd.Parameters.AddWithValue("@Amount", amount);
                        cmd.Parameters.AddWithValue("@Comment", comment);
                        cmd.Parameters.AddWithValue("@AppReference", reference);
                        cmd.ExecuteNonQuery();
                    }
                }

                timer.Stop();
                System.Diagnostics.Debug.WriteLine($"CreditPayment AppCentral ok: {timer.ElapsedMilliseconds} ms");
                return (true, null);
            }
            catch (Exception ex)
            {
                timer.Stop();
                var msg = ex.InnerException?.Message ?? ex.Message;
                System.Diagnostics.Debug.WriteLine($"CreditPayment AppCentral error after {timer.ElapsedMilliseconds} ms: {msg}");
                return (false, msg);
            }
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
    SELECT ID, AccountNumber,
           ISNULL(TaxNumber, '') AS TaxNumber,
           FirstName, LastName,
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
        [Route("api/Customers/CreditCustomers")]
        public HttpResponseMessage CreditCustomers(string criteria = null)
        {
            try
            {
                var term = (criteria ?? string.Empty).Trim();
                var connectionString = AppConfig.ConnectionString("RMHPOS");
                if (string.IsNullOrWhiteSpace(connectionString))
                    return Request.CreateResponse(HttpStatusCode.OK, new List<CustomerCreditInfoDto>());

                var list = new List<CustomerCreditInfoDto>();

                using (var cn = new SqlConnection(connectionString))
                {
                    cn.Open();

                    string sql;
                    SqlCommand cmd;

                    if (string.IsNullOrEmpty(term))
                    {
                        sql = @"
SELECT c.ID, c.AccountNumber, c.FirstName, c.LastName,
       CAST(c.PriceLevel AS INT) AS PriceLevel,
       CASE WHEN ISNULL(c.CustomText5, '0') = '0' THEN 0 ELSE 0 END AS CreditDays,
       ISNULL(bl.Amount, 0) AS ClosingBalance,
       ISNULL(a.CreditLimit, 0) AS CreditLimit,
       ISNULL(a.CreditLimit, 0) - ISNULL(bl.Amount, 0) AS Available
FROM dbo.Customer c
INNER JOIN dbo.AR_Account a ON a.Number = c.AccountNumber
LEFT JOIN dbo.AR_AccountBalance bl ON bl.ID = a.ID
WHERE ISNULL(a.CreditLimit, 0) > 0
ORDER BY c.LastName, c.FirstName";
                        cmd = new SqlCommand(sql, cn);
                    }
                    else
                    {
                        var like = "%" + term + "%";
                        sql = @"
SELECT c.ID, c.AccountNumber, c.FirstName, c.LastName,
       CAST(c.PriceLevel AS INT) AS PriceLevel,
       CASE WHEN ISNULL(c.CustomText5, '0') = '0' THEN 0 ELSE 0 END AS CreditDays,
       ISNULL(bl.Amount, 0) AS ClosingBalance,
       ISNULL(a.CreditLimit, 0) AS CreditLimit,
       ISNULL(a.CreditLimit, 0) - ISNULL(bl.Amount, 0) AS Available
FROM dbo.Customer c
INNER JOIN dbo.AR_Account a ON a.Number = c.AccountNumber
LEFT JOIN dbo.AR_AccountBalance bl ON bl.ID = a.ID
WHERE ISNULL(a.CreditLimit, 0) > 0
  AND (c.AccountNumber LIKE @term
    OR c.FirstName LIKE @term
    OR c.LastName LIKE @term
    OR c.PhoneNumber LIKE @term)
ORDER BY c.LastName, c.FirstName";
                        cmd = new SqlCommand(sql, cn);
                        cmd.Parameters.AddWithValue("@term", like);
                    }

                    cmd.CommandTimeout = 30;
                    using (cmd)
                    using (var reader = cmd.ExecuteReader())
                    {
                        while (reader.Read())
                        {
                            list.Add(new CustomerCreditInfoDto
                            {
                                ID = Convert.ToInt32(reader["ID"]),
                                AccountNumber = (reader["AccountNumber"]?.ToString() ?? string.Empty).Trim(),
                                FirstName = (reader["FirstName"]?.ToString() ?? string.Empty).Trim(),
                                LastName = (reader["LastName"]?.ToString() ?? string.Empty).Trim(),
                                AccountTypeID = Convert.ToInt32(reader["PriceLevel"]),
                                CreditDays = reader["CreditDays"] != DBNull.Value ? Convert.ToInt32(reader["CreditDays"]) : 0,
                                ClosingBalance = Convert.ToDecimal(reader["ClosingBalance"]),
                                CreditLimit = Convert.ToDecimal(reader["CreditLimit"]),
                                Available = Convert.ToDecimal(reader["Available"]),
                                HasCredit = true
                            });
                        }
                    }
                }

                return Request.CreateResponse(HttpStatusCode.OK, list);
            }
            catch (Exception ex)
            {
                var msg = ex.InnerException?.Message ?? ex.Message;
                return Request.CreateResponse(HttpStatusCode.InternalServerError, "CreditCustomers error: " + msg);
            }
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

                var connectionString = AppConfig.ConnectionString("RMHPOS");
                if (string.IsNullOrWhiteSpace(connectionString))
                    return Request.CreateResponse(HttpStatusCode.OK, new CustomerCreditInfoDto { AccountNumber = term, HasCredit = false });

                using (var cn = new SqlConnection(connectionString))
                {
                    cn.Open();

                    var sql = @"
SELECT c.ID, c.AccountNumber, c.FirstName, c.LastName,
       CAST(c.PriceLevel AS INT) AS PriceLevel,
       CASE WHEN ISNULL(c.CustomText5, '0') = '0' THEN 0 ELSE 0 END AS CreditDays,
       ISNULL(bl.Amount, 0) AS ClosingBalance,
       ISNULL(a.CreditLimit, 0) AS CreditLimit,
       ISNULL(a.CreditLimit, 0) - ISNULL(bl.Amount, 0) AS Available
FROM dbo.Customer c
INNER JOIN dbo.AR_Account a ON a.Number = c.AccountNumber
LEFT JOIN dbo.AR_AccountBalance bl ON bl.ID = a.ID
WHERE c.AccountNumber = @Acct OR c.TaxNumber = @Acct";

                    using (var cmd = new SqlCommand(sql, cn))
                    {
                        cmd.Parameters.AddWithValue("@Acct", term);
                        cmd.CommandTimeout = 15;

                        using (var reader = cmd.ExecuteReader())
                        {
                            if (reader.Read())
                            {
                                var creditLimit = Convert.ToDecimal(reader["CreditLimit"]);
                                return Request.CreateResponse(HttpStatusCode.OK, new CustomerCreditInfoDto
                                {
                                    ID = Convert.ToInt32(reader["ID"]),
                                    AccountNumber = (reader["AccountNumber"]?.ToString() ?? term).Trim(),
                                    FirstName = (reader["FirstName"]?.ToString() ?? string.Empty).Trim(),
                                    LastName = (reader["LastName"]?.ToString() ?? string.Empty).Trim(),
                                    AccountTypeID = Convert.ToInt32(reader["PriceLevel"]),
                                    CreditDays = reader["CreditDays"] != DBNull.Value ? Convert.ToInt32(reader["CreditDays"]) : 0,
                                    ClosingBalance = Convert.ToDecimal(reader["ClosingBalance"]),
                                    CreditLimit = creditLimit,
                                    Available = Convert.ToDecimal(reader["Available"]),
                                    HasCredit = creditLimit > 0
                                });
                            }
                        }
                    }
                }

                return Request.CreateResponse(HttpStatusCode.OK, new CustomerCreditInfoDto
                {
                    AccountNumber = term,
                    HasCredit = false
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
    OR TaxNumber     LIKE {0}
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

        // ──────── Obtener facturas / asientos AR abiertos del cliente ────────

        [HttpGet]
        [Route("api/Customers/OpenLedgerEntries")]
        public HttpResponseMessage OpenLedgerEntries(string accountNumber = null)
        {
            if (string.IsNullOrWhiteSpace(accountNumber))
                return Request.CreateResponse(HttpStatusCode.BadRequest, "accountNumber es requerido.");

            var connectionString = AppConfig.ConnectionString("RMHPOS");
            if (string.IsNullOrWhiteSpace(connectionString))
                return Request.CreateResponse(HttpStatusCode.InternalServerError, "No se encontró la cadena de conexión.");

            try
            {
                var entries = new List<OpenLedgerEntryDto>();
                using (var cn = new SqlConnection(connectionString))
                {
                    cn.Open();

                    // Single round-trip: resolve AccountID via INNER JOIN
                    var sql = @"
SELECT le.ID as LedgerEntryID,
       CONVERT(varchar(10), le.PostingDate, 103) as PostingDate,
       CONVERT(varchar(10), le.DueDate, 103) as DueDate,
       le.LedgerType,
       le.DocumentType,
       ISNULL(le.Description, '') as Description,
       le.StoreID,
       ISNULL(le.Reference, '') as Reference,
       ISNULL(d.Amount, 0) as Amount,
       ISNULL(d.Amount, 0) + ISNULL(applied.TotalApplied, 0) as Balance
FROM dbo.AR_LedgerEntry le
INNER JOIN dbo.AR_Account a ON a.ID = le.AccountID AND a.Number = @Number
LEFT JOIN dbo.AR_LedgerEntryDetail d
    ON d.LedgerEntryID = le.ID AND d.AppliedEntryID = 0
LEFT JOIN (
    SELECT det.AppliedEntryID, SUM(det.AppliedAmount) as TotalApplied
    FROM dbo.AR_LedgerEntryDetail det
    INNER JOIN dbo.AR_LedgerEntry le2
        ON le2.ID = det.AppliedEntryID
    INNER JOIN dbo.AR_Account a2 ON a2.ID = le2.AccountID AND a2.Number = @Number
    WHERE det.AppliedEntryID > 0
      AND le2.[Open] = 1
    GROUP BY det.AppliedEntryID
) applied ON applied.AppliedEntryID = le.ID
WHERE le.[Open] = 1
  AND le.DocumentType IN (1, 2, 3, 4)
ORDER BY le.PostingDate";

                    using (var cmd = new SqlCommand(sql, cn))
                    {
                        cmd.Parameters.AddWithValue("@Number", accountNumber.Trim());
                        cmd.CommandTimeout = 60;

                        using (var reader = cmd.ExecuteReader())
                        {
                            while (reader.Read())
                            {
                                var documentType = Convert.ToInt32(reader["DocumentType"]);
                                var ledgerType = Convert.ToInt32(reader["LedgerType"]);
                                var balance = Math.Round(Convert.ToDecimal(reader["Balance"]), 2);
                                if (balance <= 0) continue;

                                string docTypeName;
                                switch (documentType)
                                {
                                    case 1: docTypeName = "Adjustment"; break;
                                    case 2: docTypeName = "Adjustment"; break;
                                    case 3: docTypeName = ledgerType == 4 ? "Credit Memo" : "Transaction"; break;
                                    case 4: docTypeName = "Credit Memo"; break;
                                    default: docTypeName = "Other"; break;
                                }

                                string ledgerTypeName;
                                switch (ledgerType)
                                {
                                    case 1: ledgerTypeName = "Adjustment"; break;
                                    case 3: ledgerTypeName = "Invoice"; break;
                                    case 4: ledgerTypeName = "Credit Memo"; break;
                                    default: ledgerTypeName = "Other"; break;
                                }

                                entries.Add(new OpenLedgerEntryDto
                                {
                                    LedgerEntryID = Convert.ToInt32(reader["LedgerEntryID"]),
                                    PostingDate = reader["PostingDate"].ToString(),
                                    DueDate = reader["DueDate"].ToString(),
                                    LedgerTypeName = ledgerTypeName,
                                    DocumentTypeName = docTypeName,
                                    Description = reader["Description"].ToString(),
                                    StoreID = Convert.ToInt32(reader["StoreID"]),
                                    Reference = reader["Reference"].ToString(),
                                    Amount = Convert.ToDecimal(reader["Amount"]),
                                    Balance = balance
                                });
                            }
                        }
                    }
                }

                return Request.CreateResponse(HttpStatusCode.OK, entries);
            }
            catch (Exception ex)
            {
                var msg = ex.InnerException?.Message ?? ex.Message;
                return Request.CreateResponse(HttpStatusCode.InternalServerError, "OpenLedgerEntries error: " + msg);
            }
        }

        // ──────── Registrar abono a cuenta de crédito ────────

        [HttpPost]
        [Route("api/Customers/CreditPayment")]
        public HttpResponseMessage CreditPayment([FromBody] CreditPaymentRequest request)
        {
            if (request == null || string.IsNullOrWhiteSpace(request.AccountNumber) || request.Amount <= 0)
                return Request.CreateResponse(HttpStatusCode.BadRequest, new CreditPaymentResponse
                {
                    Ok = false,
                    Message = "Datos de abono inválidos."
                });

            var connectionString = AppConfig.ConnectionString("RMHPOS");
            if (string.IsNullOrWhiteSpace(connectionString))
                return Request.CreateResponse(HttpStatusCode.InternalServerError, new CreditPaymentResponse
                {
                    Ok = false,
                    Message = "No se encontró la cadena de conexión."
                });

            var appCentralCs = AppConfig.ConnectionString("AppCentralConnectionString");

            try
            {
                var accountNumber = request.AccountNumber.Trim();
                var now = DateTime.Now;
                var requestTimer = System.Diagnostics.Stopwatch.StartNew();
                var comment = (request.Comment ?? string.Empty).Trim();
                var reference = !string.IsNullOrWhiteSpace(request.Reference)
                    ? request.Reference.Trim()
                    : $"ABONO-{now:yyyyMMddHHmmss}";

                // ── 2. AR_LedgerEntry + AR_LedgerEntryDetail en RMS (RMHPOS) ──
                using (var cn = new SqlConnection(connectionString))
                {
                    cn.Open();

                    var lookupTimer = System.Diagnostics.Stopwatch.StartNew();
                    int accountID = 0;
                    int customerID = 0;
                    using (var cmd = new SqlCommand(@"
SELECT TOP 1
    a.ID AS AccountID,
    ISNULL(c.ID, 0) AS CustomerID
FROM dbo.AR_Account a
LEFT JOIN dbo.Customer c ON c.AccountNumber = a.Number
WHERE a.Number = @Number", cn))
                    {
                        cmd.Parameters.AddWithValue("@Number", accountNumber);
                        using (var reader = cmd.ExecuteReader())
                        {
                            if (reader.Read())
                            {
                                accountID = reader["AccountID"] == DBNull.Value ? 0 : Convert.ToInt32(reader["AccountID"]);
                                customerID = reader["CustomerID"] == DBNull.Value ? 0 : Convert.ToInt32(reader["CustomerID"]);
                            }
                        }
                    }
                    lookupTimer.Stop();

                    if (accountID > 0)
                    {
                        using (var tx = cn.BeginTransaction())
                        {
                            try
                            {
                                var appliedLedgerEntryIDs = new List<int>();

                                // DocumentType=5 (Payment), LedgerType=3, amount negative → reduces balance
                                byte documentType = 5;
                                byte ledgerType = 3;
                                decimal amountACY = -request.Amount; // negative = reduces debt
                                var dbTimer = System.Diagnostics.Stopwatch.StartNew();

                                // Insert AR_LedgerEntry
                                int ledgerEntryID = 0;
                                using (var cmd = new SqlCommand("dbo.OFF_AR_LEDGERENTRY_INSERT", cn, tx))
                                {
                                    cmd.CommandType = CommandType.StoredProcedure;
                                    cmd.CommandTimeout = 60;

                                    cmd.Parameters.AddWithValue("@LastUpdated", now);
                                    cmd.Parameters.AddWithValue("@AccountID", accountID);
                                    cmd.Parameters.AddWithValue("@CustomerID", customerID);
                                    cmd.Parameters.AddWithValue("@StoreID", request.StoreID);
                                    cmd.Parameters.AddWithValue("@LinkType", (byte)1);
                                    cmd.Parameters.AddWithValue("@LinkID", customerID);
                                    cmd.Parameters.AddWithValue("@AuditEntryID", 0);
                                    cmd.Parameters.AddWithValue("@DocumentType", documentType);
                                    cmd.Parameters.AddWithValue("@DocumentID", 0);
                                    cmd.Parameters.AddWithValue("@PostingDate", now);
                                    cmd.Parameters.AddWithValue("@DueDate", now);
                                    cmd.Parameters.AddWithValue("@LedgerType", ledgerType);
                                    cmd.Parameters.AddWithValue("@Reference", reference);
                                    cmd.Parameters.AddWithValue("@Description", "Abono a cuenta");
                                    cmd.Parameters.AddWithValue("@CurrencyID", 0);
                                    cmd.Parameters.AddWithValue("@CurrencyFactor", 1.0);
                                    cmd.Parameters.AddWithValue("@Positive", true);
                                    cmd.Parameters.AddWithValue("@ClosingDate", DBNull.Value);
                                    cmd.Parameters.AddWithValue("@ReasonID", 0);
                                    cmd.Parameters.AddWithValue("@HoldReasonID", 0);
                                    cmd.Parameters.AddWithValue("@UndoReasonID", 0);
                                    cmd.Parameters.AddWithValue("@PayMethodID", request.TenderID);
                                    cmd.Parameters.AddWithValue("@TransactionID", 0);
                                    cmd.Parameters.AddWithValue("@ExtReference", string.Empty);
                                    cmd.Parameters.AddWithValue("@Comment", comment);

                                    using (var reader = cmd.ExecuteReader())
                                    {
                                        if (reader.Read())
                                            ledgerEntryID = Convert.ToInt32(reader["ID"]);
                                    }
                                }

                                if (ledgerEntryID > 0)
                                {
                                    using (var cmd = new SqlCommand("dbo.OFF_AR_LEDGERENTRYDETAIL_INSERT", cn, tx))
                                    {
                                        cmd.CommandType = CommandType.StoredProcedure;
                                        cmd.CommandTimeout = 60;

                                        cmd.Parameters.AddWithValue("@LedgerEntryID", ledgerEntryID);
                                        cmd.Parameters.AddWithValue("@AccountID", accountID);
                                        cmd.Parameters.AddWithValue("@LedgerType", ledgerType);
                                        cmd.Parameters.AddWithValue("@DueDate", now);
                                        cmd.Parameters.AddWithValue("@PostingDate", now);
                                        cmd.Parameters.AddWithValue("@DetailType", (byte)0);
                                        cmd.Parameters.AddWithValue("@Reference", reference);
                                        cmd.Parameters.AddWithValue("@Amount", amountACY);
                                        cmd.Parameters.AddWithValue("@AmountLCY", amountACY);
                                        cmd.Parameters.AddWithValue("@AmountACY", amountACY);
                                        cmd.Parameters.AddWithValue("@AuditEntryID", 0);
                                        cmd.Parameters.AddWithValue("@AppliedEntryID", 0);
                                        cmd.Parameters.AddWithValue("@AppliedAmount", 0m);
                                        cmd.Parameters.AddWithValue("@UnapplyEntryID", 0);
                                        cmd.Parameters.AddWithValue("@UnapplyReasonID", 0);
                                        cmd.Parameters.AddWithValue("@ISCLOSING", false);

                                        cmd.ExecuteNonQuery();
                                    }

                                    if (request.Applications != null && request.Applications.Count > 0)
                                    {
                                        foreach (var app in request.Applications)
                                        {
                                            if (app.LedgerEntryID <= 0 || app.Amount <= 0)
                                                continue;

                                            using (var appCmd = new SqlCommand("dbo.OFF_AR_LEDGERENTRYDETAIL_INSERT", cn, tx))
                                            {
                                                appCmd.CommandType = CommandType.StoredProcedure;
                                                appCmd.CommandTimeout = 60;

                                                appCmd.Parameters.AddWithValue("@LedgerEntryID", ledgerEntryID);
                                                appCmd.Parameters.AddWithValue("@AccountID", accountID);
                                                appCmd.Parameters.AddWithValue("@LedgerType", ledgerType);
                                                appCmd.Parameters.AddWithValue("@DueDate", now);
                                                appCmd.Parameters.AddWithValue("@PostingDate", now);
                                                appCmd.Parameters.AddWithValue("@DetailType", (byte)0);
                                                appCmd.Parameters.AddWithValue("@Reference", reference);
                                                appCmd.Parameters.AddWithValue("@Amount", 0m);
                                                appCmd.Parameters.AddWithValue("@AmountLCY", 0m);
                                                appCmd.Parameters.AddWithValue("@AmountACY", 0m);
                                                appCmd.Parameters.AddWithValue("@AuditEntryID", 0);
                                                appCmd.Parameters.AddWithValue("@AppliedEntryID", app.LedgerEntryID);
                                                appCmd.Parameters.AddWithValue("@AppliedAmount", -app.Amount);
                                                appCmd.Parameters.AddWithValue("@UnapplyEntryID", 0);
                                                appCmd.Parameters.AddWithValue("@UnapplyReasonID", 0);
                                                appCmd.Parameters.AddWithValue("@ISCLOSING", false);

                                                appCmd.ExecuteNonQuery();
                                            }

                                            appliedLedgerEntryIDs.Add(app.LedgerEntryID);
                                        }
                                    }

                                    RefreshLedgerApplicationStatuses(cn, tx, ledgerEntryID, appliedLedgerEntryIDs, now);
                                }

                                tx.Commit();
                                dbTimer.Stop();
                                requestTimer.Stop();
                                System.Diagnostics.Debug.WriteLine($"CreditPayment RMHPOS ok: lookup={lookupTimer.ElapsedMilliseconds} ms, db={dbTimer.ElapsedMilliseconds} ms, apps={(request.Applications == null ? 0 : request.Applications.Count)} total={requestTimer.ElapsedMilliseconds} ms");
                            }
                            catch
                            {
                                tx.Rollback();
                                throw;
                            }
                        }
                    }
}

var (acOk, acMsg) = TryAppCentralPayment(
    appCentralCs,
    request.CashierID,
    request.StoreID,
    accountNumber,
    now,
    request.Amount,
    comment,
    reference);

return Request.CreateResponse(HttpStatusCode.OK, new CreditPaymentResponse
{
    Ok = true,
    Message = "Abono registrado correctamente.",
    RmhposOk = true,
    AppCentralOk = acOk,
    AppCentralMessage = acMsg
});
            }
            catch (Exception ex)
            {
    var msg = ex.InnerException?.Message ?? ex.Message;
    return Request.CreateResponse(HttpStatusCode.InternalServerError, new CreditPaymentResponse
    {
        Ok = false,
        Message = "Error al registrar abono: " + msg,
        RmhposOk = false,
        AppCentralOk = false,
        AppCentralMessage = "No se intentó (error en RMHPOS)."
    });
}
        }

    }
}
