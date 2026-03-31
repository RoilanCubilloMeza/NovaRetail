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

            // VE-01: PedirVendedor / VE-02: RequiereVendedor / TC-01: TipoCambio / CL-01: ClienteContadoID / CL-02: ClienteContadoNombre
            try
            {
                var posConnectionString = ConfigurationManager.ConnectionStrings["RMHPOS"]?.ConnectionString;
                if (!string.IsNullOrWhiteSpace(posConnectionString))
                {
                    using (var cn = new System.Data.SqlClient.SqlConnection(posConnectionString))
                    using (var cmd = new System.Data.SqlClient.SqlCommand(
                        "SELECT CODIGO, LTRIM(RTRIM(VALOR)) AS VALOR FROM dbo.AVS_Parametros WHERE CODIGO IN ('VE-01','VE-02','TC-01','CL-01','CL-02')", cn))
                    {
                        cn.Open();
                        using (var reader = cmd.ExecuteReader())
                        {
                            while (reader.Read())
                            {
                                var codigo = reader["CODIGO"].ToString();
                                var valorStr = reader["VALOR"]?.ToString() ?? string.Empty;
                                int valorInt;
                                int.TryParse(valorStr, out valorInt);

                                if (codigo == "VE-01") dto.AskForSalesRep = valorInt == 1;
                                if (codigo == "VE-02") dto.RequireSalesRep = valorInt == 1;
                                if (codigo == "TC-01")
                                {
                                    decimal tc;
                                    if (decimal.TryParse(valorStr, System.Globalization.NumberStyles.Number, System.Globalization.CultureInfo.InvariantCulture, out tc) && tc > 0)
                                        dto.DefaultExchangeRate = tc;
                                }
                                if (codigo == "CL-01" && !string.IsNullOrWhiteSpace(valorStr))
                                    dto.DefaultClientId = valorStr;
                                if (codigo == "CL-02" && !string.IsNullOrWhiteSpace(valorStr))
                                    dto.DefaultClientName = valorStr;
                            }
                        }
                    }
                }
            }
            catch { }

            // Impuesto por defecto: porcentaje del Tax más común
            try
            {
                var posConnectionString = ConfigurationManager.ConnectionStrings["RMHPOS"]?.ConnectionString;
                if (!string.IsNullOrWhiteSpace(posConnectionString))
                {
                    using (var cn = new System.Data.SqlClient.SqlConnection(posConnectionString))
                    using (var cmd = new System.Data.SqlClient.SqlCommand(
                        "SELECT TOP 1 Percentage FROM dbo.Tax ORDER BY ID", cn))
                    {
                        cn.Open();
                        var val = cmd.ExecuteScalar();
                        if (val != null && val != DBNull.Value)
                            dto.DefaultTaxPercentage = Convert.ToDecimal(val);
                    }
                }
            }
            catch { }

            return dto;
        }

        [HttpGet]
        [Route("api/StoreConfig/Tenders")]
        public IHttpActionResult GetTenders()
        {
            var connectionString = ConfigurationManager.ConnectionStrings["RMHPOS"]?.ConnectionString;
            if (string.IsNullOrWhiteSpace(connectionString))
                return Ok(new List<TenderDto>());

            try
            {
                var tenders = new List<TenderDto>();
                using (var cn = new SqlConnection(connectionString))
                {
                    cn.Open();

                    // Detectar si existe la columna MedioPagoCodigo en Tender
                    var hasMedioPago = false;
                    using (var colCmd = new SqlCommand(
                        "SELECT COUNT(1) FROM INFORMATION_SCHEMA.COLUMNS WHERE TABLE_NAME = 'Tender' AND COLUMN_NAME = 'MedioPagoCodigo'", cn))
                    {
                        hasMedioPago = Convert.ToInt32(colCmd.ExecuteScalar()) > 0;
                    }

                    var sql = hasMedioPago
                        ? "SELECT ID, Description, CurrencyID, DisplayOrder, ISNULL(MedioPagoCodigo,'') AS MedioPagoCodigo FROM Tender WHERE Inactive = 0 ORDER BY DisplayOrder"
                        : "SELECT ID, Description, CurrencyID, DisplayOrder, '' AS MedioPagoCodigo FROM Tender WHERE Inactive = 0 ORDER BY DisplayOrder";

                    using (var cmd = new SqlCommand(sql, cn))
                    using (var reader = cmd.ExecuteReader())
                    {
                        while (reader.Read())
                        {
                            tenders.Add(new TenderDto
                            {
                                ID = Convert.ToInt32(reader["ID"]),
                                Description = reader["Description"]?.ToString() ?? string.Empty,
                                CurrencyID = Convert.ToInt32(reader["CurrencyID"]),
                                DisplayOrder = Convert.ToInt32(reader["DisplayOrder"]),
                                MedioPagoCodigo = reader["MedioPagoCodigo"]?.ToString() ?? string.Empty
                            });
                        }
                    }
                }
                return Ok(tenders);
            }
            catch
            {
                return Ok(new List<TenderDto>());
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
        /// <summary>Porcentaje de impuesto por defecto (primer registro de Tax).</summary>
        public decimal DefaultTaxPercentage { get; set; } = 13m;
        /// <summary>Tipo de cambio por defecto (TC-01 en AVS_Parametros).</summary>
        public decimal DefaultExchangeRate { get; set; }
        /// <summary>Código del cliente contado por defecto (CL-01 en AVS_Parametros).</summary>
        public string DefaultClientId { get; set; } = "00001";
        /// <summary>Nombre del cliente contado por defecto (CL-02 en AVS_Parametros).</summary>
        public string DefaultClientName { get; set; } = "CLIENTE CONTADO";
    }

    public class TenderDto
    {
        public int ID { get; set; }
        public string Description { get; set; }
        public int CurrencyID { get; set; }
        public int DisplayOrder { get; set; }
        /// <summary>Código de medio de pago para facturación electrónica (01=Efectivo, 02=Tarjeta, 04=Transferencia, etc.).</summary>
        public string MedioPagoCodigo { get; set; } = string.Empty;
    }

    public class ConnectionInfoDto
    {
        public string DatabaseServer { get; set; }
        public string DatabaseName { get; set; }
    }
}
