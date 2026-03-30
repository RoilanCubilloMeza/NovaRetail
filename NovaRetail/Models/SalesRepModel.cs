namespace NovaRetail.Models;

public class SalesRepModel
{
    public int ID { get; set; }
    public string Number { get; set; } = string.Empty;
    public string Nombre { get; set; } = string.Empty;

    public string DisplayText => $"{Number} — {Nombre}";
}
