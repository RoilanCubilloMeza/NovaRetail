using System;
using System.Net;
using System.Net.Http;
using System.Web.Http;
using NovaAPI.Models;

namespace NovaAPI.Controllers
{
    /// <summary>
    /// Controlador de envío de correos electrónicos.
    /// Utiliza el servicio WCF <c>wsEmails.IntegraFastServiceSoapClient</c>.
    /// </summary>
    public class EmailController : ApiController
    {
        wsEmails.IntegraFastServiceSoapClient wsEmail = new wsEmails.IntegraFastServiceSoapClient();

        [Route("api/Email/EnviaEmailSinAdjuntos")]
        [HttpPost]
        public HttpResponseMessage EnviaEmailSinAdjuntos(SendEmail Email)
        {
            HttpResponseMessage msg = null;
            string registroActual = "";
            try
            {
                wsEmail.EnviaEmailSinAdjuntos(Email.smtpCode, Email.PARA, Email.CC
                    , Email.asunto, Email.cuerpoHtml, Email.token, Email.CCO);

                msg = Request.CreateResponse(HttpStatusCode.OK, "Email ha sido enviado!");
            }
            catch (Exception ex)
            {
                msg = Request.CreateResponse(HttpStatusCode.InternalServerError, "Error: " + registroActual + " / " + ex.Message.ToString());
            }

            return msg;
        }

        [Route("api/Email/EnviaEmailAdjuntosMemoryStream")]
        [HttpPost]
        public HttpResponseMessage EnviaEmailAdjuntosMemoryStream(SendEmail Email)
        {
            HttpResponseMessage msg = null;
            string registroActual = "";
            try
            {
                wsEmail.EnviaEmailAdjuntosMemoryStream(Email.smtpCode, Email.PARA, Email.CC
                    , Email.asunto, Email.cuerpoHtml, Email.NombreArchivo, Email.ListadopdfBytes, Email.CCO);

                msg = Request.CreateResponse(HttpStatusCode.OK, "Email ha sido enviado!");
            }
            catch (Exception ex)
            {
                msg = Request.CreateResponse(HttpStatusCode.InternalServerError, "Error: " + registroActual + " / " + ex.Message.ToString());
            }

            return msg;
        }
    }
}
