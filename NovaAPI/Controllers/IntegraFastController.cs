using System;
using System.Collections.Generic;
using System.Configuration;
using System.Data;
using System.Data.SqlClient;
using System.Net;
using System.ServiceModel;
using System.Web.Http;
using NovaAPI.wsEmails;

namespace NovaAPI.Controllers
{
    /// <summary>
    /// GET  api/IntegraFast/pending   → lista facturas pendientes a enviar
    /// POST api/IntegraFast/process   → procesa y envía facturas pendientes a Hacienda
    /// GET  api/IntegraFast/storeinfo → datos de tienda desde [Configuration]
    /// </summary>
    [RoutePrefix("api/IntegraFast")]
    public class IntegraFastController : ApiController
    {
        private static string ConnString =>
            AppConfig.ConnectionString("RMHPOS")
            ?? throw new ConfigurationErrorsException("Connection string RMHPOS not configured.");

        // ── WS client ─────────────────────────────────────────────────────────

        private static IntegraFastServiceSoapClient CreateWsClient()
        {
            var overrideUrl = ConfigurationManager.AppSettings["IntegraFastUrl"];
            if (!string.IsNullOrWhiteSpace(overrideUrl))
            {
                var binding = new BasicHttpBinding
                {
                    MaxBufferPoolSize = 20_000_000,
                    MaxBufferSize = 20_000_000,
                    MaxReceivedMessageSize = 20_000_000
                };
                return new IntegraFastServiceSoapClient(binding, new EndpointAddress(overrideUrl));
            }

            return new IntegraFastServiceSoapClient("IntegraFastServiceSoap");
        }

        // ── Store info ────────────────────────────────────────────────────────

        [HttpGet]
        [Route("storeinfo")]
        public IHttpActionResult GetStoreInfo()
        {
            try { return Ok(ReadStoreInfo()); }
            catch (Exception ex) { return Content(HttpStatusCode.InternalServerError, ex.Message); }
        }

        private static StoreInfoDto ReadStoreInfo()
        {
            var dto = new StoreInfoDto { CodSucursal = "001" };

            using (var cn = new SqlConnection(ConnString))
            {
                cn.Open();

                using (var cmd = new SqlCommand(
                    @"SELECT TOP 1
                        ISNULL(StoreName,'') AS StoreName,
                        ISNULL(StoreID, 1)   AS StoreID,
                        ISNULL(Address1,'')  AS Address1,
                        ISNULL(Phone,'')     AS Phone,
                        ISNULL(TaxID,'')     AS TaxID
                      FROM [Configuration]", cn))
                using (var r = cmd.ExecuteReader())
                {
                    if (r.Read())
                    {
                        dto.StoreName = r["StoreName"].ToString();
                        dto.StoreID   = r["StoreID"] != DBNull.Value ? Convert.ToInt32(r["StoreID"]) : 1;
                        dto.Address1  = r["Address1"].ToString();
                        dto.Phone     = r["Phone"].ToString();
                        dto.TaxID     = r["TaxID"].ToString();
                        dto.Terminal  = dto.StoreID.ToString().PadLeft(5, '0');
                    }
                }
            }

            return dto;
        }

        // ── Pending invoices ──────────────────────────────────────────────────

        [HttpGet]
        [Route("pending")]
        public IHttpActionResult GetPending()
        {
            try
            {
                var facturas = LoadPendingFacturas();
                return Ok(new { Count = facturas.Count, Items = facturas });
            }
            catch (Exception ex) { return Content(HttpStatusCode.InternalServerError, ex.Message); }
        }

        // ── Process pending ───────────────────────────────────────────────────

        [HttpPost]
        [Route("process")]
        public IHttpActionResult ProcessPending()
        {
            var results = new List<ProcessResultDto>();

            StoreInfoDto storeInfo;
            List<EncFactura> facturas;

            try
            {
                storeInfo = ReadStoreInfo();
                facturas  = LoadPendingFacturas();
            }
            catch (Exception ex)
            {
                return Content(HttpStatusCode.InternalServerError, $"Error al leer datos: {ex.Message}");
            }

            if (facturas.Count == 0)
                return Ok(new { Processed = 0, Results = results });

            EmisorOr emisor;
            try
            {
                emisor = CreateWsClient().GetValidaClienteExiste(storeInfo.TaxID);
            }
            catch (Exception ex)
            {
                return Content(HttpStatusCode.InternalServerError,
                    $"Error al obtener emisor del webservice: {ex.Message}");
            }

            foreach (var factura in facturas)
            {
                var result = new ProcessResultDto
                {
                    TransactionNumber = factura.ConsecutivoPos,
                    TipoDocumento     = factura.TipoDocumento
                };

                try
                {
                    // Sobreescribir Terminal y CodSucursal con valores de Configuration
                    factura.Terminal    = storeInfo.Terminal;
                    factura.CodSucursal = storeInfo.CodSucursal;

                    var receptor = BuildReceptor(factura);
                    var retorno  = CreateWsClient().CreaXMLGeneraTiqueteElectronicoPruebas(
                        _Emisor:          emisor,
                        _Receptor:        receptor,
                        _Factura:         factura,
                        _TipoComprobante: factura.TipoDocumento,
                        _Clave:           factura.Clave50,
                        _Consecutivo:     factura.Clave20,
                        Comentario1:      factura.Comentario1,
                        Comentario2:      factura.Comentario2,
                        Comentario3:      factura.Comentario3,
                        Comentario4:      factura.Comentario4,
                        Comentario5:      factura.Comentario5,
                        Comentario6:      factura.Comentario6,
                        Comentario7:      factura.Comentario7,
                        OrdenCompra:      factura.OCNum    ?? string.Empty,
                        FechaOrdenCompra: factura.OCFecha  ?? string.Empty,
                        Token:            factura.TokenUS  ?? string.Empty,
                        Token_Expiracion: factura.TokenExpiracion ?? string.Empty
                    );

                    // StatusCode "01" = aceptada por Hacienda
                    result.Ok         = retorno != null &&
                                        string.Equals(retorno.StatusCode, "01", StringComparison.Ordinal);
                    result.StatusCode = retorno?.StatusCode ?? string.Empty;
                    result.Message    = retorno?.MensajeCode ?? string.Empty;

                    MarkInvoiceSent(factura.ConsecutivoPos, result.Ok, result.Message);
                }
                catch (Exception ex)
                {
                    result.Ok      = false;
                    result.Message = ex.Message;
                    MarkInvoiceSent(factura.ConsecutivoPos, false, ex.Message);
                }

                results.Add(result);
            }

            return Ok(new { Processed = results.Count, Results = results });
        }

        // ── Private: load data ────────────────────────────────────────────────

        private static List<EncFactura> LoadPendingFacturas()
        {
            var list = new List<EncFactura>();

            using (var cn = new SqlConnection(ConnString))
            {
                cn.Open();

                using (var cmd = new SqlCommand("EXEC spAVSGetEmailToSend", cn))
                using (var rs = cmd.ExecuteReader())
                {
                    while (rs.Read())
                    {
                        var f = new EncFactura
                        {
                            TokenUS           = string.Empty,
                            TokenExpiracion   = string.Empty,
                            CodSucursal       = Safe(rs, "COD_SUCURSAL").PadLeft(3, '0'),
                            TipoDocumento     = Safe(rs, "COMPROBANTE_TIPO").PadLeft(2, '0'),
                            Clave50           = Safe(rs, "Clave50"),
                            Clave20           = Safe(rs, "Clave20"),
                            ConsecutivoPos    = SafeInt(rs, "TRANSACTIONNUMBER"),
                            Terminal          = Safe(rs, "TERMINAL_POS").PadLeft(5, '0'),
                            CodCliente        = Safe(rs, "COD_CLIENTE"),
                            NombreCliente     = Safe(rs, "NOMBRE_CLIENTE"),
                            TipoIdentificacion = Safe(rs, "TIPO_IDENTIFICACION"),
                            EmailCliente      = Safe(rs, "EMAIL"),
                            FechaEmision      = SafeDateTime(rs, "FECHA_TRANSAC"),
                            CodigoMoneda      = Safe(rs, "COD_MONEDA"),
                            TipoCambio        = Safe(rs, "TIPOCAMBIO"),
                            DiasCredito       = SafeInt(rs, "DiasCredito"),
                            CondicionVenta    = Safe(rs, "CONDICION_VENTA"),
                            Situacion         = SafeInt(rs, "COMPROBANTE_SITUACION"),
                            EnviarCliente     = SafeBool(rs, "ENVIAR_EMAIL"),
                            MedioPago1        = Safe(rs, "MEDIO_PAGO1"),
                            Provincia         = Safe(rs, "PROVINCIA"),
                            Canton            = Safe(rs, "CANTON"),
                            Distrito          = Safe(rs, "DISTRITO"),
                            Barrio            = Safe(rs, "BARRIO"),
                            OtrasSenias       = Safe(rs, "SENIAS"),
                            NumTelefono       = Safe(rs, "TELEFONO"),
                            Comentario1       = Safe(rs, "COMENTARIO1"),
                            Comentario2       = Safe(rs, "COMENTARIO2"),
                            Comentario3       = Safe(rs, "COMENTARIO3"),
                            Comentario4       = Safe(rs, "COMENTARIO4"),
                            Comentario5       = Safe(rs, "COMENTARIO5"),
                            Comentario6       = Safe(rs, "COMENTARIO6"),
                            Comentario7       = Safe(rs, "COMENTARIO7"),
                            OCFecha           = Safe(rs, "OCFecha"),
                            IdentificacionExtranjero = false
                        };

                        if (f.TipoDocumento == "03" || f.TipoDocumento == "10")
                        {
                            f.NC_RazonCodigo      = Safe(rs, "NC_CODIGO");
                            f.NC_RazonDetalle     = Safe(rs, "NC_RAZON");
                            f.NC_ReferenciaNumero = Safe(rs, "NC_REFERENCIA");
                            f.NC_TipoDocumento    = Safe(rs, "NC_TIPO_DOC");
                            var ncFecha = Safe(rs, "NC_REFERENCIA_FECHA");
                            f.NC_ReferenciaFecha  = string.IsNullOrWhiteSpace(ncFecha)
                                ? (DateTime?)null
                                : Convert.ToDateTime(ncFecha);
                        }

                        if (f.TipoDocumento == "10")
                            f.OCNum = Safe(rs, "REP_REF_TR");

                        list.Add(f);
                    }
                }

                foreach (var f in list)
                {
                    LoadDetalle(cn, f);
                    LoadTotales(cn, f);
                }
            }

            return list;
        }

        private static void LoadDetalle(SqlConnection cn, EncFactura f)
        {
            SqlCommand cmd;
            if (f.CondicionVenta == "04")
            {
                cmd = new SqlCommand("SELECT * FROM [dbo].[fxAVS_GetLineaDetalleApartado] (@ConsecutivoPos)", cn);
                cmd.Parameters.AddWithValue("@ConsecutivoPos", f.ConsecutivoPos);
            }
            else
            {
                cmd = new SqlCommand("SELECT * FROM [dbo].[fxAVS_GetLineaDetalle] (@ConsecutivoPos, @TipoDocumento)", cn);
                cmd.Parameters.AddWithValue("@ConsecutivoPos", f.ConsecutivoPos);
                cmd.Parameters.AddWithValue("@TipoDocumento", f.TipoDocumento ?? (object)DBNull.Value);
            }

            var detalles = new List<DetFactura>();
            int linea = 0;

            using (cmd)
            using (var rs2 = cmd.ExecuteReader())
            {
                while (rs2.Read())
                {
                    linea++;
                    var d = new DetFactura
                    {
                        NumLinea            = linea,
                        TipoCodigo          = "01",
                        Cabys               = Safe(rs2, "Cabys"),
                        CodPro              = Safe(rs2, "CodProducto"),
                        Detalle             = Safe(rs2, "Detalle"),
                        UnidadMedida        = Safe(rs2, "UnidadMedida"),
                        Cantidad            = SafeDecimal(rs2, "Cantidad"),
                        PrecioUnitario      = SafeDecimal(rs2, "PrecioUnitario"),
                        MontoTotal          = SafeDecimal(rs2, "MontoTotal"),
                        MontoDescuento      = SafeDecimal(rs2, "MontoDescuento"),
                        NaturalezaDescuento = Safe(rs2, "NaturalezaDescuento"),
                        SubTotal            = SafeDecimal(rs2, "SubTotal"),
                        ImpuestoCodigo      = Safe(rs2, "CodImpuesto"),
                        ImpuestoTarifa      = SafeDecimal(rs2, "TarifaIVA"),
                        ImpuestoMonto       = SafeDecimal(rs2, "MontoImpuesto"),
                        MontoTotalLinea     = SafeDecimal(rs2, "MontoLinea"),
                        FactorIVA           = SafeDecimal(rs2, "TarifaImpuesto"),
                        CodTarifaIVA        = Safe(rs2, "CodTarifaImpuesto"),
                        ImpuestoNeto        = SafeDecimal(rs2, "MontoImpuesto") - SafeDecimal(rs2, "ExoMontoExoneracion"),
                        BaseImponible       = SafeDecimal(rs2, "MontoImpuesto") - SafeDecimal(rs2, "ExoMontoExoneracion"),
                        ExoneracionTipoDocumento    = Safe(rs2, "ExoTipoDocumento"),
                        ExoneracionNumeroDocumento  = Safe(rs2, "ExoNumeroDocumento"),
                        ExoneracionNombreInstitucion = Safe(rs2, "ExoNombreInstitucion"),
                        ExoneracionPorcentajeCompra = SafeDecimal(rs2, "ExoPorcentajeExoneracion"),
                        ExoneracionMontoImpuesto    = SafeDecimal(rs2, "ExoMontoExoneracion")
                    };

                    var exoFecha = Safe(rs2, "ExoFechaEmision");
                    d.ExoneracionFechaEmision = string.IsNullOrWhiteSpace(exoFecha)
                        ? (DateTime?)null
                        : Convert.ToDateTime(exoFecha);

                    detalles.Add(d);
                }
            }

            f.Detalle = detalles.ToArray();
        }

        private static void LoadTotales(SqlConnection cn, EncFactura f)
        {
            SqlCommand cmd;
            if (f.CondicionVenta == "04")
            {
                cmd = new SqlCommand("spAVS_GetTotalesApartado", cn) { CommandType = CommandType.StoredProcedure };
                cmd.Parameters.AddWithValue("@ConsecutivoPos", f.ConsecutivoPos);
            }
            else
            {
                cmd = new SqlCommand("spAVS_GetTotales", cn) { CommandType = CommandType.StoredProcedure };
                cmd.Parameters.AddWithValue("@ConsecutivoPos", f.ConsecutivoPos);
                cmd.Parameters.AddWithValue("@TipoDocumento", f.TipoDocumento ?? (object)DBNull.Value);
            }

            using (cmd)
            using (var rs3 = cmd.ExecuteReader())
            {
                if (rs3.Read())
                {
                    f.TOTAL_SERVICIOS_GRAVADOS    = SafeDecimal(rs3, "TotalServiciosGrabados");
                    f.TOTAL_SERVICIOS_EXENTOS     = SafeDecimal(rs3, "TotalServiciosExentos");
                    f.TOTAL_MERCADERIA_GRAVADA    = SafeDecimal(rs3, "TotalMercanciasGravadas");
                    f.TOTAL_MERCADERIA_EXENTA     = SafeDecimal(rs3, "TotalMercanciasExentas");
                    f.TOTAL_GRAVADO               = SafeDecimal(rs3, "TotalGravado");
                    f.TOTAL_EXENTO                = SafeDecimal(rs3, "TotalExento");
                    f.TOTAL_DESCUENTOS            = SafeDecimal(rs3, "TotalDescuentos");
                    f.TOTAL_IMPUESTOS             = SafeDecimal(rs3, "TotalImpuesto");
                    f.TOTAL_VENTA_NETA            = SafeDecimal(rs3, "TotalVentaNeta");
                    f.TOTAL_COMPROBANTE           = SafeDecimal(rs3, "TotalComprobante");
                    f.TOTAL_VENTA                 = SafeDecimal(rs3, "TotalVenta");
                    f.TOTAL_SERVICIOS_EXONERADOS  = SafeDecimal(rs3, "TotalServExonerada");
                    f.TOTAL_MERCADERIA_EXONERADOS = SafeDecimal(rs3, "TotalMercExonerada");
                    f.TOTAL_EXONERADO             = SafeDecimal(rs3, "TotalExonerado");
                    f.TOTAL_IVA_DEVUELTO          = SafeDecimal(rs3, "TotalIVADevuelto");
                    f.TOTAL_OTROS_CARGOS          = SafeDecimal(rs3, "TotalOtrosCargos");
                }
            }
        }

        private static ReceptorFE BuildReceptor(EncFactura f)
        {
            return new ReceptorFE
            {
                Nombre         = f.NombreCliente,
                Identificacion = f.CodCliente,
                Tipo           = f.TipoIdentificacion,
                Numero         = f.CodCliente,
                CorreoElectronico = f.EmailCliente
            };
        }

        private static void MarkInvoiceSent(int transactionNumber, bool ok, string message)
        {
            try
            {
                var estado = ok ? "01" : "55";
                var obs    = ok
                    ? "ENVIADA"
                    : ("ERROR: " + (message ?? string.Empty)).Length > 200
                        ? ("ERROR: " + (message ?? string.Empty)).Substring(0, 200)
                        : "ERROR: " + (message ?? string.Empty);

                using (var cn = new SqlConnection(ConnString))
                using (var cmd = new SqlCommand(
                    "UPDATE AVS_INTEGRAFAST_01 SET ESTADO_HACIENDA=@e, OBSERVACIONES=@o WHERE TRANSACTIONNUMBER=@t", cn))
                {
                    cmd.Parameters.AddWithValue("@e", estado);
                    cmd.Parameters.AddWithValue("@o", obs);
                    cmd.Parameters.AddWithValue("@t", transactionNumber);
                    cn.Open();
                    cmd.ExecuteNonQuery();
                }
            }
            catch { /* no bloquear si la tabla no existe */ }
        }

        // ── Safe reader helpers ───────────────────────────────────────────────

        private static string   Safe(IDataReader r, string col)        { try { var v = r[col]; return v == DBNull.Value ? string.Empty : Convert.ToString(v) ?? string.Empty; } catch { return string.Empty; } }
        private static decimal  SafeDecimal(IDataReader r, string col) { try { var v = r[col]; return v == DBNull.Value ? 0m : Convert.ToDecimal(v); }  catch { return 0m; } }
        private static int      SafeInt(IDataReader r, string col)     { try { var v = r[col]; return v == DBNull.Value ? 0  : Convert.ToInt32(v); }   catch { return 0; } }
        private static bool     SafeBool(IDataReader r, string col)    { try { var v = r[col]; return v != DBNull.Value && Convert.ToBoolean(v); }      catch { return false; } }
        private static DateTime SafeDateTime(IDataReader r, string col){ try { var v = r[col]; return v == DBNull.Value ? DateTime.Now : Convert.ToDateTime(v); } catch { return DateTime.Now; } }
    }

    public class StoreInfoDto
    {
        public string StoreName   { get; set; } = string.Empty;
        public int    StoreID     { get; set; } = 1;
        public string Terminal    { get; set; } = "00001";
        public string CodSucursal { get; set; } = "001";
        public string Address1    { get; set; } = string.Empty;
        public string Phone       { get; set; } = string.Empty;
        public string TaxID       { get; set; } = string.Empty;
    }

    public class ProcessResultDto
    {
        public int    TransactionNumber { get; set; }
        public string TipoDocumento     { get; set; } = string.Empty;
        public bool   Ok                { get; set; }
        public string StatusCode        { get; set; } = string.Empty;
        public string Message           { get; set; } = string.Empty;
    }
}
