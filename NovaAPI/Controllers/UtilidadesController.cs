using System;
using System.Collections.Generic;
using System.Text;
using System.Web.Http;
using NovaAPI.Models;
using NovaAPI.wsSecurityMain;

namespace NovaAPI.Controllers
{
    public class UtilidadesController : ApiController
    {
        private readonly FacturaMeCrContractClient wsCliente =
            new FacturaMeCrContractClient("BasicHttpBinding_IFacturaMeCrContract");

        private readonly wsEmails.IntegraFastServiceSoapClient wsEmails =
            new wsEmails.IntegraFastServiceSoapClient();

        [HttpGet]
        public CommondEntitie[] GetProvincias()
        {
            return wsCliente.GetProvincias();
        }

        [HttpGet]
        public CommondEntitie[] GetCantones(int provincia)
        {
            return wsCliente.GetCantonByProvinciaId(provincia);
        }

        [HttpGet]
        public CommondEntitie[] GetDistrito(int provincia, int canton)
        {
            return wsCliente.GetDistritoByCantonIdProvinciaId(provincia, canton);
        }

        [Route("api/Utilidades/GetTipoIdentificacion")]
        [HttpGet]
        public CommondEntitie[] GetTipoIdentificacion()
        {
            return wsCliente.GetTipoIdentificacion();
        }

        [Route("api/Utilidades/Emails_Envia")]
        [HttpGet]
        public string Emails_Envia(string smtpCode, string PARA, string CC, string CCO, string asunto,
            string cuerpohtml, string token)
        {
            try
            {
                wsEmails.EnviaEmailSinAdjuntos(smtpCode, PARA, CC, asunto, cuerpohtml, token, CCO);
                return "Email ha sido enviado";
            }
            catch (Exception ex)
            {
                return "Error: " + ex.Message;
            }
        }

        [Route("api/Utilidades/HTML_Envia")]
        [HttpGet]
        public string HTML_Envia(List<OrderEntry> listaProductos, SendEmail datos, decimal subtotal, decimal descuentos,
            decimal IVA, decimal total, string nomCliente, string correo, string numReferencia, string comentarios)
        {
            try
            {
                var html = new StringBuilder();
                html.AppendLine("<!DOCTYPE html>");
                html.AppendLine("<html>");
                html.AppendLine("<head>");
                html.AppendLine("<meta http-equiv='Content-Type' content='text/html; charset=utf-8'>");
                html.AppendLine("<meta name='viewport' content='width=device-width, initial-scale=1'>");
                html.AppendLine("<meta http-equiv='X-UA-Compatible' content='IE=edge'>");
                html.AppendLine("<title></title>");
                html.AppendLine("<style type='text/css'>");
                html.AppendLine("body{background-color:#f3f5f7;margin:0;padding:0;font-family:Arial,sans-serif;}");
                html.AppendLine("h1{text-align:center;}");
                html.AppendLine("table,th,td{border:1px solid black;border-collapse:collapse;}");
                html.AppendLine("th,td{padding:5px;background-color:none;}");
                html.AppendLine("#t1{width:100%;background-color:#99E5FF;}");
                html.AppendLine("</style>");
                html.AppendLine("</head>");
                html.AppendLine("<body>");
                html.AppendLine("<h1>Generación de Pedidos</h1>");

                if (!string.IsNullOrWhiteSpace(nomCliente))
                    html.AppendLine($"<p><strong>Cliente:</strong> {nomCliente}</p>");
                if (!string.IsNullOrWhiteSpace(numReferencia))
                    html.AppendLine($"<p><strong>Referencia:</strong> {numReferencia}</p>");

                html.AppendLine("<table>");
                html.AppendLine("<thead>");
                html.AppendLine("<tr id='t1'>");
                html.AppendLine("<th scope='col'>COD</th>");
                html.AppendLine("<th scope='col'>DESCRIPCIÓN PRODUCTO/SERVICIO</th>");
                html.AppendLine("<th scope='col'>CANT</th>");
                html.AppendLine("<th scope='col'>PRECIO. UNI.</th>");
                html.AppendLine("<th scope='col'>PRECIO TOTAL</th>");
                html.AppendLine("</tr>");
                html.AppendLine("</thead>");
                html.AppendLine("<tbody>");

                if (listaProductos != null)
                {
                    foreach (var item in listaProductos)
                    {
                        html.AppendLine("<tr>");
                        html.AppendLine($"<td>{item?.ItemID}</td>");
                        html.AppendLine($"<td>{item?.Description}</td>");
                        html.AppendLine($"<td>{item?.QuantityOnOrder}</td>");
                        html.AppendLine($"<td>{item?.Price}</td>");
                        html.AppendLine($"<td>{item?.FullPrice}</td>");
                        html.AppendLine("</tr>");
                    }
                }

                html.AppendLine("</tbody>");
                html.AppendLine("</table>");
                html.AppendLine($"<p><strong>Subtotal:</strong> {subtotal}</p>");
                html.AppendLine($"<p><strong>Descuento:</strong> {descuentos}</p>");
                html.AppendLine($"<p><strong>IVA:</strong> {IVA}</p>");
                html.AppendLine($"<p><strong>Total:</strong> {total}</p>");
                html.AppendLine($"<p><strong>Comentarios:</strong> {comentarios}</p>");
                html.AppendLine("</body>");
                html.AppendLine("</html>");

                wsEmails.EnviaEmailSinAdjuntos(
                    "test",
                    correo,
                    correo,
                    "Generación de  Orden",
                    html.ToString(),
                    "YMZ38azu4UM?2pe=e?c$RNj#",
                    "acamacho@facturamecr.com");

                return "Email ha sido enviado";
            }
            catch (Exception ex)
            {
                return "Error: " + ex.Message;
            }
        }
    }
}
