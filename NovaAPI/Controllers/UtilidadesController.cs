using System;
using System.Collections.Generic;
using System.Web.Http;
using NovaAPI.Models;
using NovaAPI.wsSecurityMain;

namespace NovaAPI.Controllers
{
    public class UtilidadesController : ApiController
    {
        wsSecurityMain.FacturaMeCrContractClient wsCliente = new wsSecurityMain.FacturaMeCrContractClient("BasicHttpBinding_IFacturaMeCrContract");
        wsEmails.IntegraFastServiceSoapClient wsEmails = new wsEmails.IntegraFastServiceSoapClient();

        //readonly LINQDataContext db = new LINQDataContext();

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

        /*        [HttpGet]
                public ISingleResult<spAVSAccesoWooCommerceResult> GetCredencialesWC(string tokenUs)
                {
                    if (tokenUs == "Q2Qv/UdNK8tM6wvzg0Qfyg==")
                    {
                        return db.spAVSAccesoWooCommerce();
                    }
                    else
                    {
                        return null;
                    }

                }*/


        [Route("api/Utilidades/Emails_Envia")]
        [HttpGet]
        public string Emails_Envia(string smtpCode, string PARA, string CC, string CCO, string asunto,
                                                string cuerpohtml, string token)
        {
            string msg = null;
            string registroActual = "";
            try
            {

                wsEmails.EnviaEmailSinAdjuntos(smtpCode, PARA, CC, asunto, cuerpohtml, token, CCO);

                msg = "Email ha sido enviado";


            }
            catch (Exception ex)
            {
                msg = "Error: " + registroActual + " / " + ex.Message.ToString();
            }

            return msg;
        }

        [Route("api/Utilidades/HTML_Envia")]
        [HttpGet]
        public string HTML_Envia(List<OrderEntry> listaProductos, SendEmail datos, decimal subtotal, decimal descuentos, decimal IVA, decimal total, string nomCliente, string correo, string numReferencia, string comentarios)
        {
            string msg = null;
            string registroActual = "";

            try
            {
                #region CREACION Y CARGA PLANTILLA HTML
                #region HEAD
                string Html = "<!DOCTYPE html>";
                Html += "<html>";
                Html += "<head>";
                Html += "<title></title>";
                Html += "<meta http-equiv ='Content-Type'content='text/html; charset=utf-8'>";
                Html += "<meta name='viewport' content='width=device-width, initial-scale=1'>";
                Html += "<meta http-equiv='X-UA-Compatible' content='IE=edge'>";
                #endregion
                #region STYLE
                Html += "<STYLE type = 'text/css'>";
                Html += "H1{text-align:center}";
                Html += "table, th, td {";
                Html += "border: 1px solid black;}";
                Html += "        th, td { padding: 5px; background-color:none; }";
                Html += "#t1 {";
                Html += "width: 100 %;";
                Html += "background-color:#99E5FF;}";
                Html += "</STYLE>";
                #endregion
                Html += "</head>";
                Html += "<body style = 'background -color: #f3f5f7; margin: 0 !important; padding: 0 !important;'>";

                Html += "<H1>Generación de Pedidos</H1>";
                //Html += "<p><strong>Cliente:</strong> " + nomCliente + "</p>";
                //Html += "<p><strong>Num Referencia:</strong> " + numReferencia + "</p>";

                Html += "    <div>";
                Html += "        <table>";
                Html += "            <thead class='table-header'>";
                Html += "              <tr id='t1'>";
                Html += "                <th scope='col'>COD</th>";
                Html += "                <th scope='col'>DESCRIPCIÓN PRODUCTO/SERVICIO</th>";
                Html += "                <th scope='col'>CANT</th>";
                Html += "                <th scope='col'>PRECIO. UNI.</th>";
                Html += "                <th scope='col'>PRECIO TOTAL</th>";
                Html += "              </tr>";
                Html += "            </thead>";
                Html += "            <tbody>";

                //for (int i = 0; i < listaProductos.Count; i++)
                //{
                //    Html += "              <tr>";
                //    Html += "                <td>" + listaProductos[i].ID + "</td>";
                //    Html += "                <td>" + listaProductos[i].Description + "</td>";
                //    Html += "                <td>" + listaProductos[i].QuantityOnOrder + "</td>";
                //    Html += "                <td>" + listaProductos[i].Price + "</td>";
                //    Html += "                <td>" + listaProductos[i].FullPrice + "</td>";
                //    Html += "              </tr>";
                //}

                Html += "            </tbody>";
                Html += "          </table>";
                Html += "    </div>";

                #region MONTOS
                Html += "<p><strong>Subtotal:</strong> " + subtotal + "</p>";
                Html += "<p><strong>Descuento:</strong> " + descuentos + "</p>";
                Html += "<p><strong>IVA:</strong> " + IVA + "</p>";
                Html += "<p><strong>Total:</strong> " + total + "</p>";
                Html += "<p><strong>Comentarios:</strong> " + comentarios + "</p>";
                Html += "</body>";
                Html += "</html>";
                #endregion

                #region Plantilla

                //string Html = "<!DOCTYPE html PUBLIC ' -//W3C//DTD XHTML 1.0 Transitional//EN' 'http://www.w3.org/TR/xhtml1/DTD/xhtml1-transitional.dtd'>";
                //Html += "<html xmlns='http://www.w3.org/1999/xhtml' xmlns:o='urn:schemas-microsoft-com:office:office'>";

                //Html += "<head>";
                //Html += "<meta charset='UTF - 8'>";
                //Html += "<meta content='width = device - width, initial - scale = 1' name='viewport'>";
                //Html += "<meta name='x - apple - disable - message - reformatting'>";
                //Html += "<meta http-equiv='X - UA - Compatible' content='IE = edge'>";
                //Html += "<meta content='telephone = no' name='format - detection'>";
                //Html += "<title></title>";

                //Html += "<link href='https://fonts.googleapis.com/css?family=Open+Sans:400,400i,700,700i' rel='stylesheet'>";

                //Html += "</head>";

                //Html += "<body>";
                //Html += "<div class='es - wrapper - color'>";

                //Html += "<table class='es - wrapper' width='100%' cellspacing='0' cellpadding='0'>";
                //Html += "<tbody>";
                //Html += "<tr>";
                //Html += "<td class='esd - email - paddings' valign='top'>";

                //Html += "<table class='es - content' cellspacing='0' cellpadding='0' align='center'>";
                //Html += "<tbody>";
                //Html += "<tr></tr>";
                //Html += "<tr>";
                //Html += "<td class='esd - stripe' esd-custom-block-id='7681' align='center'>";
                //Html += "<table class='es - header - body' style='background - color: #044767;' width='600' cellspacing='0' cellpadding='0' bgcolor='#044767' align='center'>";
                //Html += "<tbody>";
                //Html += "<tr>";
                //Html += "<td class='esd - structure es - p35t es - p35b es - p35r es - p35l' align='left'>";
                //Html += "<table class='es - left' cellspacing='0' cellpadding='0' align='left'>";
                //Html += "<tbody>";
                //Html += "<tr>";
                //Html += "<td class='es - m - p0r es - m - p20b esd - container - frame' width='340' valign='top' align='center'>";
                //Html += "<table width='100 %' cellspacing='0' cellpadding='0'>";
                //Html += "<tbody>";
                //Html += "<tr>";
                //Html += "<Html += 'td class='esd-block-text es-m-txt-c' align='left'>";
                //Html += "<h1 style='color: #ffffff; line-height: 100%;'>Distribuidora San Diego</h1>";
                //Html += "</td>";
                //Html += "</tr>";
                //Html += "</tbody>";
                //Html += "</table>";
                //Html += "</td>";
                //Html += "</tr>";
                //Html += "</tbody>";
                //Html += "</table>";
                //Html += "<table cellspacing='0' cellpadding='0' align='right'>";
                //Html += "<tbody>";
                //Html += "<tr class='es-hidden'>";
                //Html += "<td class='es-m-p20b esd-container-frame' esd-custom-block-id='7704' width='170' align='left'>";
                //Html += "<table width='100%' cellspacing='0' cellpadding='0'>";
                //Html += "<tbody>";
                //Html += "<tr>";
                //Html += "<td class='esd-block-spacer es-p5b' align='center' style='font-size:0'>";
                //Html += "<table width='100%' height='100%' cellspacing='0' cellpadding='0' border='0'>";
                //Html += "<tbody>";
                //Html += "<tr>";
                //Html += "<td style='border-bottom: 1px solid #044767; background: rgba(0, 0, 0, 0) none repeat scroll 0% 0%; height: 1px; width: 100%; margin: 0px;'></td>";
                //Html += "</tr>";
                //Html += "</tbody>";
                //Html += "</table>";
                //Html += "</td>";
                //Html += "</tr>";

                //Html += "</tbody>";
                //Html += "</table>";
                //Html += "</td>";
                //Html += "</tr>";
                //Html += "</tbody>";
                //Html += "</table>";
                //Html += "</td>";
                //Html += "</tr>";
                //Html += "</tbody>";
                //Html += "</table>";
                //Html += "</td>";
                //Html += "</tr>";
                //Html += "</tbody>";
                //Html += "</table>";
                //Html += "<table class='es-content' cellspacing='0' cellpadding='0' align='center'>";
                //Html += "<tbody>";
                //Html += "<tr>";
                //Html += "<td class='esd-stripe' align='center'>";
                //Html += "<table class='es-content-body' width='600' cellspacing='0' cellpadding='0' bgcolor='#ffffff' align='center'>";
                //Html += "<tbody>";
                //Html += "<tr>";
                //Html += "<td class='esd-structure es-p40t es-p35r es-p35l' align='left'>";
                //Html += "<table width='100%' cellspacing='0' cellpadding='0'>";
                //Html += "<tbody>";
                //Html += "<tr>";
                //Html += "<td class='esd-container-frame' width='530' valign='top' align='center'>";
                //Html += "<table width='100%' cellspacing='0' cellpadding='0'>";
                //Html += "<tbody>";
                //Html += "<tr>";
                //Html += "<td class='esd-block-image es-p25t es-p25b es-p35r es-p35l' align='center' style='font-size:0'><a target='_blank' href='https://viewstripo.email/'><img src='https://tlr.stripocdn.email/content/guids/CABINET_75694a6fc3c4633b3ee8e3c750851c02/images/67611522142640957.png' alt style='display: block;' width='120'></a></td>";
                //Html += "</tr>";
                //Html += "<tr>";
                //Html += "<td class='esd-block-text es-p10b' align='center'>";
                //Html += "<h2>Generación de Pedidos!</h2>";
                //Html += "</td>";
                //Html += "</tr>";
                //Html += "<tr>";
                //Html += "<td class='esd-block-text es-p15t es-p20b' align='left'>";
                //Html += "<p style='font-size: 16px;'>Cliente:" + nomCliente + "<br></p>";
                //Html += "</td>";
                //Html += "</tr>";
                //Html += "</tbody>";
                //Html += "</table>";
                //Html += "</td>";
                //Html += "</tr>";
                //Html += "</tbody>";
                //Html += "</table>";
                //Html += "</td>";
                //Html += "</tr>";
                //Html += "</tbody>";
                //Html += "</table>";
                //Html += "</td>";
                //Html += "</tr>";
                //Html += "</tbody>";
                //Html += "</table>";
                //Html += "<table class='es-content' cellspacing='0' cellpadding='0'>";
                //Html += "<tbody>";
                //Html += "<tr>";
                //Html += "<td class='esd-stripe' align='center'>";
                //Html += "<table class='es-content-body' width='590' cellspacing='0' cellpadding='0' bgcolor='#ffffff' align='center'>";
                //Html += "<tbody>";
                //Html += "<tr>";
                //Html += "<td class='esd-structure es-p20t es-p35r es-p35l' align='center'>";
                //Html += "<table width='100%' cellspacing='0' cellpadding='0'>";
                //Html += "<tbody>";
                //Html += "<tr>";
                //Html += "<td class='esd-container-frame' width='530' valign='top'>";
                //Html += "<table width='100%' cellspacing='0' cellpadding='0'>";
                //Html += "<tbody>";
                //Html += "<tr>";
                //Html += "<td class='esd-block-text es-p10t es-p10b es-p10r es-p10l' bgcolor='#eeeeee'>";
                //Html += "<table style='width: 500px;' class='cke_show_border' cellspacing='1' cellpadding='1' border='0'>";
                //Html += "<tbody>";
                //Html += "<tr>";
                //Html += "<td width='15%' style='padding: 0px 10px 0px 10px'>";
                //Html += "<h4>COD</h4>";
                //Html += "</td>";
                //Html += "<td width='60%' style='padding: 0px 70px 0px 0px'>";
                //Html += "<h4>DESCRIPCIÓN</h4>";
                //Html += "</td>";
                //Html += "<td width='10%' style='padding: 0px 25px 0px 0px' align='center'>";
                //Html += "<h4>CANT</h4>";
                //Html += "</td>";
                //Html += "<td width='15%' style='padding: 0px 10px 0px 0px' align='center'>";
                //Html += "<h4>PRECIO. UNI.</h4>";
                //Html += "</td>";
                //Html += "<td width='15%' style='padding: 0px 95px 0px 0px' align='center'>";
                //Html += "<h4>PRECIO TOTAL</h4>";
                //Html += "</td>";
                //Html += "</tr>";
                //Html += "</tbody>";
                //Html += "</table>";
                //Html += "</td>";
                //Html += "</tr>";
                //Html += "</tbody>";
                //Html += "</table>";
                //Html += "</td>";
                //Html += "</tr>";
                //Html += "</tbody>";
                //Html += "</table>";
                //Html += "</td>";
                //Html += "</tr>";
                //Html += "<tr>";
                //Html += "<td class='esd-structure es-p35r es-p35l' align='center'>";
                //Html += "<table width='100%' cellspacing='0' cellpadding='0'>";
                //Html += "<tbody>";
                //Html += "<tr>";
                //Html += "<td class='esd-container-frame' width='530' valign='top' align='center'>";
                //Html += "<table width='100%' cellspacing='0' cellpadding='0'>";
                //Html += "<tbody>";
                //Html += "<tr>";
                //Html += "<td class='esd-block-text es-p10t es-p10b es-p10r es-p10l' align='center'>";
                //Html += "<table style='width: 500px;' class='cke_show_border' cellspacing='1' cellpadding='1' border='0' align='center'>";
                //Html += "<tbody>";
                //// Html += "<tr>";
                //for (int i = 0; i < listaProductos.Count; i++)
                //{
                //    Html += "              <tr>";
                //    Html += "                <td width='15%'>" + listaProductos[i].ItemID + "</td>";
                //    Html += "                <td width='60%' align='center'>" + listaProductos[i].Description + "</td>";
                //    Html += "                <td width='10%' align='center'>" + listaProductos[i].QuantityOnOrder + "</td>";
                //    Html += "                <td width='15%' align='center'>" + listaProductos[i].Price + "</td>";
                //    Html += "                <td width='15%' align='center'>" + listaProductos[i].FullPrice + "</td>";
                //    Html += "              </tr>";
                //}


                //Html += "</tbody>";
                //Html += "</table>";
                //Html += "</td>";
                //Html += "</tr>";
                //Html += "</tbody>";
                //Html += "</table>";
                //Html += "</td>";
                //Html += "</tr>";
                //Html += "</tbody>";
                //Html += "</table>";
                //Html += "</td>";
                //Html += "</tr>";
                //Html += "<tr>";
                //Html += "<td class='esd-structure es-p10t es-p35r es-p35l' align='left'>";
                //Html += "<table width='100%' cellspacing='0' cellpadding='0'>";
                //Html += "<tbody>";
                //Html += "<tr>";
                //Html += "<td class='esd-container-frame' width='530' valign='top' align='center'>";
                //Html += "<table style='border-top: 3px solid #eeeeee; border-bottom: 3px solid #eeeeee;' width='100%' cellspacing='0' cellpadding='0'>";
                //Html += "<tbody>";
                //Html += "<tr>";
                //Html += "<td class='esd-block-text es-p15t es-p15b es-p10r es-p10l' align='left'>";
                //Html += "<table style='width: 500px;' class='cke_show_border' cellspacing='1' cellpadding='1' border='0' align='left'>";
                //Html += "<tbody>";
                //Html += "<tr>";
                //Html += "<td width='80%'>";
                //Html += "<h4>Subtotal</h4>";
                //Html += "</td>";
                //Html += "<td width='20%'>";
                //Html += "<h4>196,495.00</h4>";
                //Html += "</td>";
                //Html += "</tr>";
                //Html += "<tr>";
                //Html += "<td width='80%'>";
                //Html += "<h4>Descuentos</h4>";
                //Html += "</td>";
                //Html += "<td width='20%'>";
                //Html += "<h4>0.00</h4>";
                //Html += "</td>";
                //Html += "</tr>";
                //Html += "<tr>";
                //Html += "<td width='80%'>";
                //Html += "<h4>IVA</h4>";
                //Html += "</td>";
                //Html += "<td width='20%'>";
                //Html += "<h4>0.00</h4>";
                //Html += "</td>";
                //Html += "</tr>";
                //Html += "<tr>";
                //Html += "<td width='80%'>";
                //Html += "<h4>TOTAL</h4>";
                //Html += "</td>";
                //Html += "<td width='20%'>";
                //Html += "<h4>196,495.00</h4>";
                //Html += "</td>";
                //Html += "</tr>";
                //Html += "</tbody>";
                //Html += "</table>";
                //Html += "</td>";
                //Html += "</tr>";
                //Html += "</tbody>";
                //Html += "</table>";
                //Html += "</td>";
                //Html += "</tr>";
                //Html += "</tbody>";
                //Html += "</table>";
                //Html += "</td>";
                //Html += "</tr>";

                //Html += "</tbody>";
                //Html += "</table>";
                //Html += "</td>";
                //Html += "</tr>";
                //Html += "</tbody>";
                //Html += "</table>";



                //Html += "</td>";
                //Html += "</tr>";
                //Html += "</tbody>";
                //Html += "</table>";
                //Html += "</div>";
                //Html += "</body>";

                //Html += "</html>";

                #endregion


                #endregion
                wsEmails.EnviaEmailSinAdjuntos("test", correo, correo, "Generación de  Orden", Html, "YMZ38azu4UM?2pe=e?c$RNj#", "acamacho@facturamecr.com");

                msg = "Email ha sido enviado";

            }
            catch (Exception ex)
            {
                msg = "Error: " + registroActual + " / " + ex.Message.ToString();
            }

            return msg;



        }

    }
}
