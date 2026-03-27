using System;
using System.Collections.Generic;
using System.Configuration;
using System.Linq;
using System.Web.Http;

namespace NovaAPI.Controllers
{
    /// <summary>
    /// Controlador de códigos de motivo (ReasonCode).
    /// Tipos: 3 = Override de Precio, 4 = Descuento, 5 = Nota de Crédito, 6 = Exoneración.
    /// </summary>
    public class ReasonCodesController : ApiController
    {
        readonly RMHCDataContext db = new RMHCDataContext(ConfigurationManager.ConnectionStrings["RMHPOS"].ConnectionString);

        /// <summary>
        /// Devuelve los códigos de motivo de un tipo específico.
        /// Ejemplos comunes: 4 = descuentos, 5 = notas de crédito, 6 = exoneraciones.
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
    }

    /// <summary>
    /// DTO liviano de códigos de motivo.
    /// Se usa para exponer al frontend el identificador, tipo y descripción de cada razón configurable en RMH.
    /// </summary>
    public class ReasonCodeDto
    {
        public int ID { get; set; }
        public int Type { get; set; }
        public string Code { get; set; }
        public string Description { get; set; }
    }
}
