using System;
using System.Collections.Generic;
using System.Configuration;
using System.Data.SqlClient;
using System.Linq;
using System.Web.Http;

namespace NovaAPI.Controllers
{
    /// <summary>
    /// GET api/StoreConfig         → configuración fiscal y de cotizaciones
    /// GET api/StoreConfig/Tenders → formas de pago activas con su moneda
    /// </summary>
    public class StoreConfigController : ApiController
    {
        readonly RMHCDataContext db = new RMHCDataContext(
            ConfigurationManager.ConnectionStrings["RMHPOS"].ConnectionString);

        [HttpGet]
        public StoreConfigDto Get()
        {
            var dto = new StoreConfigDto();

            try
            {
                var result = db.ExecuteQuery<StoreConfigDto>(
                    "SELECT TOP 1 TaxSystem, QuoteExpirationDays, DefaultTenderID FROM [Configuration]")
                    .FirstOrDefault();
                if (result != null)
                {
                    dto.TaxSystem = result.TaxSystem;
                    dto.QuoteExpirationDays = result.QuoteExpirationDays;
                    dto.DefaultTenderID = result.DefaultTenderID;
                }
            }
            catch { }

            try
            {
                var posConnectionString = ConfigurationManager.ConnectionStrings["RMHPOS"]?.ConnectionString;
                if (!string.IsNullOrWhiteSpace(posConnectionString))
                {
                    using (var cn = new System.Data.SqlClient.SqlConnection(posConnectionString))
                    using (var cmd = new System.Data.SqlClient.SqlCommand(
                        "SELECT TOP 1 StoreID FROM dbo.Batch WHERE Status IN (0, 2, 4, 6) ORDER BY BatchNumber DESC", cn))
                    {
                        cn.Open();
                        var scalar = cmd.ExecuteScalar();
                        if (scalar != null && scalar != DBNull.Value)
                            dto.StoreID = Convert.ToInt32(scalar);
                    }
                }
            }
            catch { }

            return dto;
        }

        [HttpGet]
        [Route("api/StoreConfig/Tenders")]
        public IEnumerable<TenderDto> GetTenders()
        {
            try
            {
                return db.ExecuteQuery<TenderDto>(
                    "SELECT ID, Description, CurrencyID, DisplayOrder FROM Tender WHERE Inactive = 0 ORDER BY DisplayOrder");
            }
            catch
            {
                return new List<TenderDto>();
            }
        }

        [HttpGet]
        [Route("api/StoreConfig/ConnectionInfo")]
        public ConnectionInfoDto GetConnectionInfo()
        {
            try
            {
                var connectionString = ConfigurationManager.ConnectionStrings["RMHPOS"]?.ConnectionString ?? string.Empty;
                var builder = new SqlConnectionStringBuilder(connectionString);

                return new ConnectionInfoDto
                {
                    DatabaseServer = builder.DataSource,
                    DatabaseName = builder.InitialCatalog
                };
            }
            catch
            {
                return new ConnectionInfoDto();
            }
        }
    }

    public class StoreConfigDto
    {
        public int StoreID { get; set; }
        public int TaxSystem { get; set; }
        public int QuoteExpirationDays { get; set; }
        public int DefaultTenderID { get; set; }
    }

    public class TenderDto
    {
        public int ID { get; set; }
        public string Description { get; set; }
        public int CurrencyID { get; set; }
        public int DisplayOrder { get; set; }
    }

    public class ConnectionInfoDto
    {
        public string DatabaseServer { get; set; }
        public string DatabaseName { get; set; }
    }
}
