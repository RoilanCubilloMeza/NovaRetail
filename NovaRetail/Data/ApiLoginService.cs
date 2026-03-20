using System.Net.Http.Json;
using System.Text;
using System.Text.Json;
using NovaRetail.Models;

namespace NovaRetail.Data;

public class ApiLoginService : ILoginService
{
    private const string AuthClientName = "NovaAuth";
    private static readonly string[] BaseUrls =
    {
        "http://localhost:52500"
    };

    private readonly IHttpClientFactory _httpClientFactory;

    public ApiLoginService(IHttpClientFactory httpClientFactory)
    {
        _httpClientFactory = httpClientFactory;
    }

    public async Task<LoginUserModel?> LoginAsync(string userName, string password)
    {
        var login = userName?.Trim() ?? string.Empty;
        var clave = password?.Trim() ?? string.Empty;

        var isDatabaseConnected = await IsDatabaseConnectedAsync();
        if (!isDatabaseConnected)
            return null;

        if (string.IsNullOrWhiteSpace(login))
            return null;

        foreach (var baseUrl in BaseUrls)
        {
            try
            {
                var http = _httpClientFactory.CreateClient(AuthClientName);

                // Intentar POST primero (más seguro: credenciales en body)
                try
                {
                    var postUrl = $"{baseUrl}/api/Login";
                    var payload = new { ID_CLIENTE = 1, LOGIN = login, CLAVE = clave, TOKEN = "" };
                    var content = new StringContent(JsonSerializer.Serialize(payload), Encoding.UTF8, "application/json");
                    var postResponse = await http.PostAsync(postUrl, content);
                    if (postResponse.IsSuccessStatusCode)
                    {
                        var result = await postResponse.Content.ReadFromJsonAsync<ApiLoginResponse>();
                        if (result is not null && !string.IsNullOrWhiteSpace(result.US_LOGIN))
                        {
                            return new LoginUserModel
                            {
                                ClientId = result.ID_CLIENTE,
                                UserName = result.US_LOGIN ?? login,
                                DisplayName = string.IsNullOrWhiteSpace(result.US_NOMBRE) ? login : result.US_NOMBRE,
                                StoreId = result.US_ID_STORE ?? 0
                            };
                        }
                    }
                }
                catch
                {
                    // POST no soportado, caer al GET como fallback
                }

                // Fallback: GET (compatibilidad con API existente)
                var getUrl = $"{baseUrl}/api/Login?ID_CLIENTE=1&LOGIN={Uri.EscapeDataString(login)}&CLAVE={Uri.EscapeDataString(clave)}&TOKEN=";
                var getResult = await http.GetFromJsonAsync<ApiLoginResponse>(getUrl);
                if (getResult is not null && !string.IsNullOrWhiteSpace(getResult.US_LOGIN))
                {
                    return new LoginUserModel
                    {
                        ClientId = getResult.ID_CLIENTE,
                        UserName = getResult.US_LOGIN ?? login,
                        DisplayName = string.IsNullOrWhiteSpace(getResult.US_NOMBRE) ? login : getResult.US_NOMBRE,
                        StoreId = getResult.US_ID_STORE ?? 0
                    };
                }
            }
            catch
            {
            }
        }

        return null;
    }

    public async Task<bool> IsDatabaseConnectedAsync()
    {
        foreach (var baseUrl in BaseUrls)
        {
            try
            {
                var http = _httpClientFactory.CreateClient(AuthClientName);
                var url = $"{baseUrl}/api/StoreConfig";
                var response = await http.GetAsync(url);
                if (response.IsSuccessStatusCode)
                    return true;
            }
            catch
            {
            }
        }

        return false;
    }

    public async Task<LoginConnectionInfoModel?> GetConnectionInfoAsync()
    {
        foreach (var baseUrl in BaseUrls)
        {
            try
            {
                var http = _httpClientFactory.CreateClient(AuthClientName);
                var url = $"{baseUrl}/api/StoreConfig/ConnectionInfo";
                var result = await http.GetFromJsonAsync<ApiConnectionInfoResponse>(url);
                if (result is null)
                    continue;

                return new LoginConnectionInfoModel
                {
                    ApiBaseUrl = baseUrl,
                    DatabaseServer = result.DatabaseServer ?? string.Empty,
                    DatabaseName = result.DatabaseName ?? string.Empty,
                    IsConnected = true
                };
            }
            catch
            {
            }
        }

        return new LoginConnectionInfoModel
        {
            ApiBaseUrl = BaseUrls[0],
            DatabaseServer = string.Empty,
            DatabaseName = "BM",
            IsConnected = false
        };
    }

    private sealed class ApiLoginResponse
    {
        public int ID_CLIENTE { get; set; }
        public string? US_LOGIN { get; set; }
        public string? US_NOMBRE { get; set; }
        public int? US_ID_STORE { get; set; }
    }

    private sealed class ApiConnectionInfoResponse
    {
        public string? DatabaseServer { get; set; }
        public string? DatabaseName { get; set; }
    }
}
