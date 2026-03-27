using System;
using System.Collections.Generic;
using System.Configuration;
using System.IO;
using System.Linq;
using System.Web.Http;
using System.Web.Services;

namespace NovaAPI.Controllers
{
    /// <summary>
    /// Controlador de medios/imágenes.
    /// Gestiona el listado de imágenes de productos almacenadas en el servidor local.
    /// </summary>
    public class MediaController : ApiController
    {

        /// <summary>
        /// Devuelve el listado de imágenes disponibles para un cliente o contexto dado.
        /// El resultado normalmente contiene el nombre del archivo y la ruta accesible para consumirlo.
        /// </summary>
        [Route("api/Media/GetListadoImagenes")]
        [HttpGet]
        public Dictionary<string, string> GetListadoImagenes(string IDCliente, string tipo)
        {
            Dictionary<string, string> ListadoImagenes = new Dictionary<string, string>();

            if (tipo == "URL_WEB")
            {
                WebService data = new WebService();

                // Ruta base local donde se almacenan las imágenes del cliente o contexto solicitado.
                var URL = data.Server.MapPath(ConfigurationManager.AppSettings["UbicacionFiles"].ToString() + "/" + IDCliente);

                var extensions = new String[] { "jpg", "png" };

                if (!Directory.Exists(URL))
                {
                    Directory.CreateDirectory(URL);
                }

                // Obtiene las carpetas de productos disponibles dentro de la ruta base.
                var ItemsFolder = Directory.GetDirectories(URL).ToList();

                for (int i = 0; i <= ItemsFolder.Count() - 1; i++)
                {
                    var filesInFolder = Directory.GetFiles(ItemsFolder[i].ToString()).ToList();

                    // Recorre cada carpeta y devuelve la lista plana de imágenes encontradas.
                    for (int j = 0; j <= filesInFolder.Count() - 1; j++)
                    {
                        string Rutaimagen = filesInFolder[j].ToString();

                        ListadoImagenes.Add(Rutaimagen, Rutaimagen);
                    }
                }

            }

            return ListadoImagenes;
        }

        [Route("api/Media/GetListadoImagenesProducto")]
        [HttpGet]
        public Dictionary<string, Dictionary<string, List<string>>> GetListadoImagenesProducto(string IDCliente, string tipo)
        {
            Dictionary<string, List<string>> ListadoImagenes = new Dictionary<string, List<string>>();
            var extensions = new String[] { "jpg", "png" };

            if (tipo == "URL_WEB")
            {
                WebService data = new WebService();

                //Esta ruta es en el servidor local, en el caso de correr el API con local Host deben crear la ruta
                //en el equipo local
                var URL = data.Server.MapPath(ConfigurationManager.AppSettings["UbicacionFiles"].ToString() + "/" + IDCliente);

                var URLImagen = ConfigurationManager.AppSettings["URL_Server"].ToString() + "/" + IDCliente;

                //var URLImagen = "https://demo.facturamecr.com/RestConn/Content/Xamarin/FacturaMeCRApp/Clients/"+ IDCliente;
                if (!Directory.Exists(URLImagen))
                {
                    Directory.CreateDirectory(URL);
                }
                //Obtiene el listado de Carpetas (Productos) disponibles en la ruta deseada
                var ItemsFolder = Directory.GetDirectories(URL).ToList();

                for (int i = 0; i <= ItemsFolder.Count() - 1; i++)
                {
                    //Obtiene la información de cada Folder
                    //     string rutaFolder = URL + "/" + ItemsFolder[i].ToString();
                    var filesInFolder = Directory.GetFiles(ItemsFolder[i].ToString()).ToList();
                    filesInFolder = filesInFolder.Where(x => x.ToLower().Contains(extensions[0]) || x.ToLower().Contains(extensions[1])).ToList();
                    for (int j = 0; j < filesInFolder.Count; j++)
                    {
                        filesInFolder[j] = filesInFolder[j].Replace(URL, URLImagen);
                        filesInFolder[j] = filesInFolder[j].Replace("\\", "/");
                    }
                    ItemsFolder[i] = ItemsFolder[i].Replace(URL + "\\", "");

                    ListadoImagenes.Add(ItemsFolder[i], new List<string>(filesInFolder));
                }
            }
            Dictionary<string, Dictionary<string, List<string>>> ListadoImagenesProducto = new Dictionary<string, Dictionary<string, List<string>>>();
            ListadoImagenesProducto.Add(IDCliente, ListadoImagenes);
            return ListadoImagenesProducto;
        }

    }

}