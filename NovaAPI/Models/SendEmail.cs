using NovaAPI.wsEmails;

namespace NovaAPI.Models
{
    public class SendEmail
    {
        public string smtpCode { get; set; }
        public string PARA { get; set; }
        public string CC { get; set; }
        public string CCO { get; set; }
        public string asunto { get; set; }
        public string cuerpoHtml { get; set; }
        public string token { get; set; }


        //Se utilizan para el evento EnviaEmailAdjuntosMemoryStream
        public ArrayOfString NombreArchivo { get; set; }
        public ArrayOfBase64Binary ListadopdfBytes { get; set; }
    }
}