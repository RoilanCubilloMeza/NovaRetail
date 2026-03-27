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

        /// <summary>
        /// Envía un correo HTML sin adjuntos.
        /// Se usa para notificaciones simples o comprobantes cuyo contenido viaja solo en el cuerpo del mensaje.
        /// </summary>
        [Route("api/Email/EnviaEmailSinAdjuntos")]
        [HttpPost]
        public HttpResponseMessage EnviaEmailSinAdjuntos(SendEmail Email)
        {
            HttpResponseMessage msg = null;
            try
            {
                wsEmail.EnviaEmailSinAdjuntos(Email.smtpCode, Email.PARA, Email.CC
                    , Email.asunto, Email.cuerpoHtml, Email.token, Email.CCO);

                msg = Request.CreateResponse(HttpStatusCode.OK, "Email ha sido enviado!");
            }
            catch (Exception ex)
            {
                msg = Request.CreateResponse(HttpStatusCode.InternalServerError, "Error al enviar correo sin adjuntos: " + ex.Message);
            }

            return msg;
        }

        /// <summary>
        /// Envía un correo con adjuntos cargados en memoria.
        /// Está pensado para documentos generados por la aplicación sin necesidad de archivos temporales en disco.
        /// </summary>
        [Route("api/Email/EnviaEmailAdjuntosMemoryStream")]
        [HttpPost]
        public HttpResponseMessage EnviaEmailAdjuntosMemoryStream(SendEmail Email)
        {
            HttpResponseMessage msg = null;
            try
            {
                wsEmail.EnviaEmailAdjuntosMemoryStream(Email.smtpCode, Email.PARA, Email.CC
                    , Email.asunto, Email.cuerpoHtml, Email.NombreArchivo, Email.ListadopdfBytes, Email.CCO);

                msg = Request.CreateResponse(HttpStatusCode.OK, "Email ha sido enviado!");
            }
            catch (Exception ex)
            {
                msg = Request.CreateResponse(HttpStatusCode.InternalServerError, "Error al enviar correo con adjuntos: " + ex.Message);
            }

            return msg;
        }
    }
}
