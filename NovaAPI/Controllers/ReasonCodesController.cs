using System;
using System.Collections.Generic;
using System.Configuration;
using System.Web.Http;

namespace NovaAPI.Controllers
{
    public class ReasonCodesController : ApiController
    {
        readonly RMHCDataContext db = new RMHCDataContext(AppConfig.ConnectionString("RMHPOS"));

        /// <summary>
        /// GET api/ReasonCodes?type=4   → Descuentos
        /// GET api/ReasonCodes?type=5   → Notas de Crédito
        /// GET api/ReasonCodes?type=6   → Exoneraciones
        /// </summary>
        [HttpGet]
        public IEnumerable<ReasonCodeDto> Get(int type)
        {
            try
            {
                return db.ExecuteQuery<ReasonCodeDto>(
                    "SELECT ID, Type, Code, Description FROM ReasonCode WHERE Type = {0} ORDER BY Code",
                    type);
            }
            catch
            {
                return new List<ReasonCodeDto>();
            }
        }

        [HttpGet]
        [Route("api/ReasonCodes/exoneration-document-types")]
        public IEnumerable<ReasonCodeDto> GetExonerationDocumentTypes()
        {
            try
            {
                return db.ExecuteQuery<ReasonCodeDto>(
                    "SELECT 0 AS ID, 0 AS Type, Code, Description FROM ExonerationDocumentType ORDER BY Code");
            }
            catch
            {
                return new List<ReasonCodeDto>();
            }
        }
    }

    public class ReasonCodeDto
    {
        public int ID { get; set; }
        public int Type { get; set; }
        public string Code { get; set; }
        public string Description { get; set; }
    }
}
