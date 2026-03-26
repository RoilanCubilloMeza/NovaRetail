namespace NovaRetail.Models;

/// <summary>
/// Datos del cliente para facturación electrónica costarricense.
/// Incluye cédula, tipo de identificación, dirección (provincia/cantón/distrito/barrio),
/// tipo de cliente (nivel de precio), y código de actividad económica.
/// </summary>
public class ClienteModel
{
    public string ClientId { get; set; } = string.Empty;
    public string IdType { get; set; } = "Cédula Física";
    public string Name { get; set; } = string.Empty;
    public bool IsReceiver { get; set; }
    public string Phone { get; set; } = string.Empty;
    public string Email { get; set; } = string.Empty;
    public string Email2 { get; set; } = string.Empty;
    public string? Province { get; set; }
    public string? Canton { get; set; }
    public string? District { get; set; }
    public string? Barrio { get; set; }
    public string CustomerType { get; set; } = string.Empty;
    public string Address { get; set; } = string.Empty;
    public string ActivityCode { get; set; } = string.Empty;
    public List<string> ActivityCodes { get; set; } = new();
    public string ActivityDescription { get; set; } = string.Empty;
}
