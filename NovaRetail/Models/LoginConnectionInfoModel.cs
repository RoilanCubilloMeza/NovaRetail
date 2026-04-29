namespace NovaRetail.Models;

public class LoginConnectionInfoModel
{
    public string ApiBaseUrl { get; set; } = string.Empty;
    public string DatabaseServer { get; set; } = string.Empty;
    public string DatabaseName { get; set; } = string.Empty;
    public bool IsConnected { get; set; }
}
