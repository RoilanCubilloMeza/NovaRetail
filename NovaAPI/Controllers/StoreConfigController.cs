using System;
using System.Collections.Generic;
using System.Configuration;
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
            try
            {
                return db.ExecuteQuery<StoreConfigDto>(
                    "SELECT TOP 1 TaxSystem, QuoteExpirationDays, DefaultTenderID FROM [Configuration]")
                    .FirstOrDefault() ?? new StoreConfigDto();
            }
            catch
            {
                return new StoreConfigDto();
            }
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
    }

    public class StoreConfigDto
    {
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
}
