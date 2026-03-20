namespace NovaAPI.Models
{
    public class LoginRequest
    {
        public int ID_CLIENTE { get; set; }
        public string LOGIN { get; set; }
        public string CLAVE { get; set; }
        public string TOKEN { get; set; }
    }
}
