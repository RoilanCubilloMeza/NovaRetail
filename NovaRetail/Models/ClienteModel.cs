namespace NovaRetail.Models
{
    public class ClienteModel
    {
        public string ClientId { get; set; } = string.Empty;
        public string IdType { get; set; } = "Cédula Física";
        public string Name { get; set; } = string.Empty;
        public bool IsReceiver { get; set; }
        public string Phone { get; set; } = string.Empty;
        public string Email { get; set; } = string.Empty;
        public string? Province { get; set; }
        public string? Canton { get; set; }
        public string? District { get; set; }
        public string Address { get; set; } = string.Empty;
        public string ActivityCode { get; set; } = string.Empty;
        public string ActivityDescription { get; set; } = string.Empty;
    }
}
