using System;
using System.Collections.Generic;
using System.Configuration;
using System.Data.SqlClient;
using System.Net;
using System.Net.Http;
using System.Web.Http;

namespace NovaAPI.Controllers
{
    /// <summary>
    /// CRUD para la tabla AVS_Parametros y lectura/escritura de ExtTender_Settings.
    /// Solo roles autorizados deben consumir estos endpoints.
    /// </summary>
    public class ParametrosController : ApiController
    {
        private readonly string _cs = AppConfig.ConnectionString("RMHPOS");

        // ───── AVS_Parametros ─────

        /// <summary>GET api/Parametros → todos los parámetros</summary>
        [HttpGet]
        public IHttpActionResult Get()
        {
            var list = new List<ParametroDto>();
            try
            {
                using (var cn = new SqlConnection(_cs))
                {
                    cn.Open();
                    using (var cmd = new SqlCommand(
                        "SELECT CODIGO, ISNULL(DESCRIPCION,'') AS DESCRIPCION, ISNULL(LTRIM(RTRIM(VALOR)),'') AS VALOR FROM dbo.AVS_Parametros ORDER BY CODIGO", cn))
                    using (var r = cmd.ExecuteReader())
                    {
                        while (r.Read())
                        {
                            list.Add(new ParametroDto
                            {
                                Codigo = r["CODIGO"].ToString(),
                                Descripcion = r["DESCRIPCION"].ToString(),
                                Valor = r["VALOR"].ToString()
                            });
                        }
                    }
                }
            }
            catch (Exception ex)
            {
                return InternalServerError(ex);
            }

            return Ok(list);
        }

        /// <summary>PUT api/Parametros → actualizar un parámetro existente</summary>
        [HttpPut]
        public IHttpActionResult Put([FromBody] ParametroDto dto)
        {
            if (dto == null || string.IsNullOrWhiteSpace(dto.Codigo))
                return BadRequest("Código es requerido.");

            try
            {
                using (var cn = new SqlConnection(_cs))
                {
                    cn.Open();
                    using (var cmd = new SqlCommand(
                        @"IF EXISTS (SELECT 1 FROM dbo.AVS_Parametros WHERE CODIGO = @cod)
                              UPDATE dbo.AVS_Parametros SET VALOR = @val, DESCRIPCION = @desc WHERE CODIGO = @cod
                          ELSE
                              INSERT INTO dbo.AVS_Parametros (CODIGO, DESCRIPCION, VALOR) VALUES (@cod, @desc, @val)", cn))
                    {
                        cmd.Parameters.AddWithValue("@cod", dto.Codigo.Trim());
                        cmd.Parameters.AddWithValue("@val", (dto.Valor ?? string.Empty).Trim());
                        cmd.Parameters.AddWithValue("@desc", (dto.Descripcion ?? string.Empty).Trim());
                        cmd.ExecuteNonQuery();
                    }
                }
            }
            catch (Exception ex)
            {
                return InternalServerError(ex);
            }

            return Ok(dto);
        }

        // ───── ExtTender_Settings ─────

        /// <summary>GET api/Parametros/Tenders → configuración de tenders</summary>
        [Route("api/Parametros/Tenders")]
        [HttpGet]
        public IHttpActionResult GetTenders()
        {
            try
            {
                using (var cn = new SqlConnection(_cs))
                {
                    cn.Open();
                    using (var cmd = new SqlCommand(
                        @"SELECT TOP 1 ID,
                            ISNULL(SalesTenderCods,'') AS SalesTenderCods,
                            ISNULL(PaymentsTenderCods,'') AS PaymentsTenderCods,
                            ISNULL(NCTenderCods,'') AS NCTenderCods,
                            ISNULL(NCPaymentCods,'') AS NCPaymentCods,
                            ISNULL(NCPaymentChargeCode,'') AS NCPaymentChargeCode
                          FROM dbo.ExtTender_Settings", cn))
                    using (var r = cmd.ExecuteReader())
                    {
                        if (r.Read())
                        {
                            return Ok(new TenderSettingsDto
                            {
                                ID = Convert.ToInt32(r["ID"]),
                                SalesTenderCods = r["SalesTenderCods"].ToString(),
                                PaymentsTenderCods = r["PaymentsTenderCods"].ToString(),
                                NCTenderCods = r["NCTenderCods"].ToString(),
                                NCPaymentCods = r["NCPaymentCods"].ToString(),
                                NCPaymentChargeCode = r["NCPaymentChargeCode"].ToString()
                            });
                        }
                    }
                }
            }
            catch (Exception ex)
            {
                return InternalServerError(ex);
            }

            return NotFound();
        }

        /// <summary>PUT api/Parametros/Tenders → actualizar configuración de tenders</summary>
        [Route("api/Parametros/Tenders")]
        [HttpPut]
        public IHttpActionResult PutTenders([FromBody] TenderSettingsDto dto)
        {
            if (dto == null)
                return BadRequest("Datos requeridos.");

            try
            {
                using (var cn = new SqlConnection(_cs))
                {
                    cn.Open();
                    using (var cmd = new SqlCommand(
                        @"IF EXISTS (SELECT 1 FROM dbo.ExtTender_Settings WHERE ID = @id)
                              UPDATE dbo.ExtTender_Settings
                                 SET SalesTenderCods    = @sales,
                                     PaymentsTenderCods = @payments,
                                     NCTenderCods       = @nc,
                                     NCPaymentCods      = @ncpay,
                                     NCPaymentChargeCode = @nccharge
                               WHERE ID = @id
                          ELSE
                              INSERT INTO dbo.ExtTender_Settings (SalesTenderCods, PaymentsTenderCods, NCTenderCods, NCPaymentCods, NCPaymentChargeCode)
                              VALUES (@sales, @payments, @nc, @ncpay, @nccharge)", cn))
                    {
                        cmd.Parameters.AddWithValue("@id", dto.ID > 0 ? dto.ID : 1);
                        cmd.Parameters.AddWithValue("@sales", (dto.SalesTenderCods ?? string.Empty).Trim());
                        cmd.Parameters.AddWithValue("@payments", (dto.PaymentsTenderCods ?? string.Empty).Trim());
                        cmd.Parameters.AddWithValue("@nc", (dto.NCTenderCods ?? string.Empty).Trim());
                        cmd.Parameters.AddWithValue("@ncpay", (dto.NCPaymentCods ?? string.Empty).Trim());
                        cmd.Parameters.AddWithValue("@nccharge", (dto.NCPaymentChargeCode ?? string.Empty).Trim());
                        cmd.ExecuteNonQuery();
                    }
                }
            }
            catch (Exception ex)
            {
                return InternalServerError(ex);
            }

            return Ok(dto);
        }
    }

    public class ParametroDto
    {
        public string Codigo { get; set; }
        public string Descripcion { get; set; }
        public string Valor { get; set; }
    }

    public class TenderSettingsDto
    {
        public int ID { get; set; }
        public string SalesTenderCods { get; set; }
        public string PaymentsTenderCods { get; set; }
        public string NCTenderCods { get; set; }
        public string NCPaymentCods { get; set; }
        public string NCPaymentChargeCode { get; set; }
    }
}
