using System;
using System.Net;
using System.Net.Http;
using System.Web.Http;
using NovaAPI.Models;
using NovaAPI.wsSecurityMain;

namespace NovaAPI.Controllers
{
    public class LoginController : ApiController
    {
        wsSecurityMain.FacturaMeCrContractClient wsCliente = new wsSecurityMain.FacturaMeCrContractClient();
        /*
        [HttpGet]
        public F_Cliente_AppEntitie Get(int ID_CLIENTE, string LOGIN, string CLAVE, string TOKEN)
        {
            try
            {
                return wsCliente.ValidaCliente_App(ID_CLIENTE, LOGIN, CLAVE, TOKEN); 
            }
            catch (Exception)
            {
                return null;
            }
        }*/

        [HttpGet]
        public Cliente_App Get(int ID_CLIENTE, string LOGIN, string CLAVE, string TOKEN)
        {
            Cliente_App usuario = new Cliente_App();
            using (var client = new FacturaMeCrContractClient())
            {
                var resultado = wsCliente.ValidaCliente_App(ID_CLIENTE, LOGIN, CLAVE, TOKEN);
                if (resultado != null)
                {
                    usuario.ID_CLIENTE = resultado.ID_CLIENTE;
                    usuario.CEDULA_CLIENTE = resultado.CEDULA_CLIENTE;
                    usuario.DEV_MODELO = resultado.DEV_MODELO;
                    usuario.DEV_NAME = resultado.DEV_NAME;
                    usuario.DEV_SERIAL_PHONE = resultado.DEV_SERIAL_PHONE;
                    usuario.DEV_VERSION = resultado.DEV_VERSION;
                    usuario.DIR_FISICA = resultado.DIR_FISICA;
                    usuario.US_EMAIL = resultado.US_EMAIL;
                    usuario.EMAIL_CC = resultado.EMAIL_CC;
                    usuario.EMAIL_CCO = resultado.EMAIL_CCO;
                    usuario.EMAIl_EMPRESA = resultado.EMAIl_EMPRESA;
                    usuario.EMAIL_INVOICE = resultado.EMAIL_INVOICE;
                    usuario.EMAIL_ORDER_IND = resultado.EMAIL_ORDER_IND;
                    usuario.EMAIL_ORDER_RESUMEN = resultado.EMAIL_ORDER_RESUMEN;
                    usuario.EMAIL_QUOTE_IND = resultado.EMAIL_QUOTE_IND;
                    usuario.EMAIL_QUOTE_RESUMEN = resultado.EMAIL_QUOTE_RESUMEN;

                    usuario.NOMBRE_COMERCIAL = resultado.NOMBRE_COMERCIAL;
                    usuario.NOTAS_GENERAL = resultado.NOTAS_GENERAL;
                    usuario.NOTAS_ORDENES = resultado.NOTAS_ORDENES;
                    usuario.SMTP_CODIGO = resultado.SMTP_CODIGO;
                    usuario.SMTP_DEVICE = resultado.SMTP_DEVICE;
                    usuario.TELEFONO = resultado.TELEFONO;
                    usuario.US_CLAVE = resultado.US_CLAVE;
                    usuario.US_COD_VENDEDOR = resultado.US_COD_VENDEDOR;
                    usuario.US_ESTADO = resultado.US_ESTADO;
                    usuario.US_GET_CLIENTE_VENDEDOR = resultado.US_GET_CLIENTE_VENDEDOR;
                    usuario.US_DESCUENTOS_MODO = resultado.US_DESCUENTOS_MODO;
                    usuario.US_IDIOMA = resultado.US_IDIOMA;
                    usuario.US_LOGIN = resultado.US_LOGIN;
                    usuario.US_NOMBRE = resultado.US_NOMBRE;
                    usuario.US_OFFLINE = resultado.US_OFFLINE;
                    usuario.US_SYNC_CLIENTES = resultado.US_SYNC_CLIENTES;
                    usuario.US_SYNC_COTIZA = resultado.US_SYNC_COTIZA;
                    usuario.US_SYNC_ORDENES = resultado.US_SYNC_ORDENES;
                    usuario.US_SYNC_PRODUCTOS = resultado.US_SYNC_PRODUCTOS;
                    usuario.US_SYNC_VENTAS = resultado.US_SYNC_VENTAS;
                    usuario.US_TOKEN = resultado.US_TOKEN;
                    usuario.US_TOKEN_FECHA_VENCE = resultado.US_TOKEN_FECHA_VENCE;
                    usuario.US_URL_API_LOCAL = resultado.US_URL_API_LOCAL;
                    usuario.US_URL_API_UNIVERSAL = resultado.US_URL_API_UNIVERSAL;
                    usuario.US_URL_IMAGENES = resultado.US_URL_IMAGENES;

                    usuario.US_ORDEN_VER_PRECIOS = resultado.US_ORDEN_VER_PRECIOS;
                    usuario.US_ORDEN_VER_TOTALES = resultado.US_ORDEN_VER_TOTALES;
                    usuario.US_ORDEN_FORMATO = resultado.US_ORDEN_FORMATO;
                    usuario.US_HORARIO_ID = resultado.US_HORARIO_ID;

                    usuario.LOGO_URL = resultado.LOGO_URL;
                    usuario.PR_CONSEC_COT = resultado.PR_CONSEC_COT;
                    usuario.PR_CONSEC_OP = resultado.PR_CONSEC_OP;
                    usuario.PR_CONSEC_RECIBOS = resultado.PR_CONSEC_RECIBOS;

                    usuario.AC_LOGO_PARTNER = resultado.AC_LOGO_PARTNER;
                    usuario.AC_NOMBRE_PARTNER = resultado.AC_NOMBRE_PARTNER;
                    usuario.US_ALERTA_PROMOS = resultado.US_ALERTA_PROMOS;
                    usuario.US_CLIENTES_CREA = resultado.US_CLIENTES_CREA;
                    usuario.US_CLIENTES_EDITA = resultado.US_CLIENTES_EDITA;
                    usuario.US_DESC_GLOBAL = resultado.US_DESC_GLOBAL;
                    usuario.US_FOOTER_OP = resultado.US_FOOTER_OP;
                    usuario.US_FOOTER_PROFORMA = resultado.US_FOOTER_PROFORMA;
                    usuario.US_FORMATO_RECIBOS = resultado.US_FORMATO_RECIBOS;
                    usuario.US_LVL_PRICE = resultado.US_LVL_PRICE;
                    usuario.US_PERMITE_CENTRAL = resultado.US_PERMITE_CENTRAL;
                    usuario.US_SYNC_FREC = resultado.US_SYNC_FREC;
                    usuario.US_URL_CENTRAL = resultado.US_URL_CENTRAL;

                    usuario.US_FORMATO_OP = resultado.US_FORMATO_OP;
                    usuario.US_FOOTER_VENTAS = resultado.US_FOOTER_VENTAS;
                    usuario.US_FORMATO_PROFORMA = resultado.US_FORMATO_PROFORMA;


                    usuario.US_ID_STORE = Convert.ToInt32(resultado.US_STOREID);


                    //Convierte de Binary a Byte

                    usuario.IMAGEN_LOGO = "data:image/png;base64," + Convert.ToBase64String(resultado.IMAGEN_LOGO.ToArray(), 0, resultado.IMAGEN_LOGO.Length);
                }
                else
                {
                    usuario = null;
                }
            }
            return usuario;

        }


        [Route("api/Login/PostUpdate")]
        [HttpPost]
        public HttpResponseMessage PostUpdate(Cliente_App Cliente)
        {
            HttpResponseMessage msg = null;
            string registroActual = "";
            try
            {
                var resultado = wsCliente.RegistraCliente_App(Cliente.ID_CLIENTE, Cliente.US_LOGIN, Cliente.US_TOKEN
                    , Cliente.DEV_MODELO, Cliente.DEV_NAME, Cliente.DEV_VERSION, Cliente.DEV_SERIAL_PHONE);

                msg = Request.CreateResponse(HttpStatusCode.OK, resultado);
            }
            catch (Exception ex)
            {
                msg = Request.CreateResponse(HttpStatusCode.InternalServerError, "Error: " + registroActual + " / " + ex.Message.ToString());
            }

            return msg;
        }
    }
}
