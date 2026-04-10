using System;

namespace NovaAPI.Models
{
    public class Cliente_App
    {

        public int ID_CLIENTE { get; set; }
        public string CEDULA_CLIENTE { get; set; }
        public string US_LOGIN { get; set; }
        public string US_NOMBRE { get; set; }
        public string US_CLAVE { get; set; }
        public string US_EMAIL { get; set; }
        public string US_TOKEN { get; set; }
        public string US_IDIOMA { get; set; }
        public short? US_OFFLINE { get; set; }
        public short? US_SYNC_CLIENTES { get; set; }
        public short? US_SYNC_PRODUCTOS { get; set; }
        public short? US_SYNC_ORDENES { get; set; }
        public short? US_SYNC_COTIZA { get; set; }
        public short? US_SYNC_VENTAS { get; set; }
        public string US_COD_VENDEDOR { get; set; }
        public string US_GET_CLIENTE_VENDEDOR { get; set; }
        public string US_DESCUENTOS_MODO { get; set; }
        public string US_URL_IMAGENES { get; set; }
        public string US_URL_API_UNIVERSAL { get; set; }
        public string US_URL_API_LOCAL { get; set; }
        public DateTime? US_TOKEN_FECHA_VENCE { get; set; }
        public string DEV_MODELO { get; set; }
        public string DEV_NAME { get; set; }
        public string DEV_VERSION { get; set; }
        public string DEV_SERIAL_PHONE { get; set; }
        public short? SMTP_DEVICE { get; set; }
        public string SMTP_CODIGO { get; set; }
        public string EMAIL_CC { get; set; }
        public string EMAIL_CCO { get; set; }
        public short? EMAIL_ORDER_IND { get; set; }
        public short? EMAIL_ORDER_RESUMEN { get; set; }
        public short? EMAIL_QUOTE_IND { get; set; }
        public short? EMAIL_QUOTE_RESUMEN { get; set; }
        public short? EMAIL_INVOICE { get; set; }
        public short? US_ESTADO { get; set; }

        //Datos de la empresa a la que pertenece el usuario

        public string NOMBRE_COMERCIAL { get; set; }
        public string TELEFONO { get; set; }
        public string DIR_FISICA { get; set; }
        public string EMAIl_EMPRESA { get; set; }
        public string IMAGEN_LOGO { get; set; }
        public string NOTAS_GENERAL { get; set; }
        public string NOTAS_ORDENES { get; set; }

        //Información de Formatos y permisos

        public short? US_ORDEN_VER_PRECIOS { get; set; }
        public short? US_ORDEN_VER_TOTALES { get; set; }
        public string US_ORDEN_FORMATO { get; set; }
        public int? US_HORARIO_ID { get; set; }


        // Información Agregada el 03/11/2020 - Rmonge
        public string LOGO_URL { get; set; }
        public string PR_CONSEC_RECIBOS { get; set; }
        public string PR_CONSEC_COT { get; set; }
        public string PR_CONSEC_OP { get; set; }

        //Información agregada 2021/07/07 - Ktorres
        public string AC_LOGO_PARTNER { get; set; }
        public string AC_NOMBRE_PARTNER { get; set; }
        public short? US_ALERTA_PROMOS { get; set; }
        public int? US_ID_STORE { get; set; }
        public short? US_CLIENTES_CREA { get; set; }
        public short? US_CLIENTES_EDITA { get; set; }
        public short? US_DESC_GLOBAL { get; set; }
        public short? US_FOOTER_OP { get; set; }
        public short? US_FOOTER_PROFORMA { get; set; }
        public short? US_FOOTER_VENTAS { get; set; }
        public short? US_FORMATO_OP { get; set; }
        public short? US_FORMATO_PROFORMA { get; set; }
        public short? US_FORMATO_RECIBOS { get; set; }
        public short? US_LVL_PRICE { get; set; }
        public short? US_PERMITE_CENTRAL { get; set; }
        public short? US_SYNC_FREC { get; set; }
        public string US_URL_CENTRAL { get; set; }

        // Rol y autorización
        public string US_ROLE_CODE { get; set; }
        public string US_ROLE_NAME { get; set; }
        public short US_SECURITY_LEVEL { get; set; }
        public int US_PRIVILEGES { get; set; }
        public string US_ROLE_PRIVILEGES { get; set; }

    }
}