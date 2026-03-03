namespace NovaAPI.Models
{
    public class Ubicaciones
    {
        public int ID { get; set; }
        public string RutaID { get; set; }
        public int CustomerID { get; set; }
        public string Nombre { get; set; }
        public string Descripcion { get; set; }
        public double Latitud { get; set; }
        public double Longitud { get; set; }
        public int Tipo { get; set; }

    }
}