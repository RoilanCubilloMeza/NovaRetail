using System.Collections.Generic;

namespace NovaAPI.Models
{
    public class SyncLocation
    {
        public List<Rutas> Routes { get; set; }
        public List<Ubicaciones> Locations { get; set; }
    }
}