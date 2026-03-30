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
                var posConnectionString = ConfigurationManager.ConnectionStrings["RMHPOS"]?.ConnectionString;
                if (!string.IsNullOrWhiteSpace(posConnectionString))
                {
                    using (var cn = new SqlConnection(posConnectionString))
                    using (var cmd = new SqlCommand(
                        "SELECT TOP 1 TaxSystem, QuoteExpirationDays, DefaultTenderID FROM dbo.[Configuration]", cn))
                    {
                        cn.Open();
                        using (var reader = cmd.ExecuteReader())
                        {
                            if (reader.Read())
                            {
                                dto.TaxSystem = reader["TaxSystem"] != DBNull.Value ? Convert.ToInt32(reader["TaxSystem"]) : 0;
                                dto.QuoteExpirationDays = reader["QuoteExpirationDays"] != DBNull.Value ? Convert.ToInt32(reader["QuoteExpirationDays"]) : 0;
                                dto.DefaultTenderID = reader["DefaultTenderID"] != DBNull.Value ? Convert.ToInt32(reader["DefaultTenderID"]) : 0;
                            }
                        }
                    }
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
                        @"SELECT TOP 1 BatchNumber, StoreID, RegisterID
                          FROM dbo.Batch
                          WHERE ClosingTime IS NULL
                            AND Status IN (0, 2, 4, 6)
                          ORDER BY OpeningTime DESC, BatchNumber DESC", cn))
                    {
                        cn.Open();
                        using (var reader = cmd.ExecuteReader())
                        {
                            if (reader.Read())
                            {
                                dto.BatchNumber = reader["BatchNumber"] != DBNull.Value ? Convert.ToInt32(reader["BatchNumber"]) : 0;
                                dto.StoreID = reader["StoreID"] != DBNull.Value ? Convert.ToInt32(reader["StoreID"]) : 0;
                                dto.RegisterID = reader["RegisterID"] != DBNull.Value ? Convert.ToInt32(reader["RegisterID"]) : 0;
                            }
                        }
                    }
                }
            }
            catch { }

            try
            {
                var posConnectionString = ConfigurationManager.ConnectionStrings["RMHPOS"]?.ConnectionString;
                if (!string.IsNullOrWhiteSpace(posConnectionString) && dto.StoreID > 0)
                {
                    using (var cn = new System.Data.SqlClient.SqlConnection(posConnectionString))
                    using (var cmd = new System.Data.SqlClient.SqlCommand(
                        @"SELECT TOP 1
                            ISNULL(Name,'')     AS StoreName,
                            ISNULL(Address1,'') AS Addr1,
                            ISNULL(Address2,'') AS Addr2,
                            ISNULL(Phone,'')    AS Phone
                          FROM dbo.Store
                          WHERE StoreID = @sid", cn))
                    {
                        cmd.Parameters.AddWithValue("@sid", dto.StoreID);
                        cn.Open();
                        using (var reader = cmd.ExecuteReader())
                        {
                            if (reader.Read())
                            {
                                dto.StoreName    = reader["StoreName"].ToString();
                                dto.StoreAddress = $"{reader["Addr1"]} {reader["Addr2"]}".Trim();
                                dto.StorePhone   = reader["Phone"].ToString();
                            }
                        }
                    }
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
                        "SELECT TOP 1 CAST(VALOR AS INT) FROM dbo.AVS_Parametros WHERE CODIGO = 'PR-01'", cn))
                    {
                        cn.Open();
                        var val = cmd.ExecuteScalar();
                        if (val != null && val != DBNull.Value)
                            dto.PriceOverridePriceSource = Convert.ToInt32(val);
                    }
                }
            }
            catch { }

            // VE-01: PedirVendedor / VE-02: RequiereVendedor
            try
            {
                var posConnectionString = ConfigurationManager.ConnectionStrings["RMHPOS"]?.ConnectionString;
                if (!string.IsNullOrWhiteSpace(posConnectionString))
                {
                    using (var cn = new System.Data.SqlClient.SqlConnection(posConnectionString))
                    using (var cmd = new System.Data.SqlClient.SqlCommand(
                        "SELECT CODIGO, CAST(VALOR AS INT) AS VALOR FROM dbo.AVS_Parametros WHERE CODIGO IN ('VE-01','VE-02')", cn))
                    {
                        cn.Open();
                        using (var reader = cmd.ExecuteReader())
                        {
                            while (reader.Read())
                            {
                                var codigo = reader["CODIGO"].ToString();
                                var valor = Convert.ToInt32(reader["VALOR"]);
                                if (codigo == "VE-01") dto.AskForSalesRep = valor == 1;
                                if (codigo == "VE-02") dto.RequireSalesRep = valor == 1;
                            }
                        }
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
        public int RegisterID { get; set; }
        public int BatchNumber { get; set; }
        public int TaxSystem { get; set; }
        public int QuoteExpirationDays { get; set; }
        public int DefaultTenderID { get; set; }
        public string StoreName { get; set; } = string.Empty;
        public string StoreAddress { get; set; } = string.Empty;
        public string StorePhone { get; set; } = string.Empty;
        /// <summary>PriceSource a usar cuando el precio se sobreescribe hacia arriba (CODIGO = 'PR-01' en AVS_Parametros).</summary>
        public int PriceOverridePriceSource { get; set; } = 1;
        /// <summary>VE-01: Si se debe pedir vendedor al iniciar sesión.</summary>
        public bool AskForSalesRep { get; set; }
        /// <summary>VE-02: Si el vendedor es obligatorio para facturar.</summary>
        public bool RequireSalesRep { get; set; }
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
