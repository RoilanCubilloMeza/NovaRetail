using System.Net.Http.Json;
using System.Text;
using System.Text.Json;
using Microsoft.Extensions.Logging;
using NovaRetail.Models;

namespace NovaRetail.Data;

public sealed class ApiLoginService : ILoginService
{
    private const string AuthClientName = "NovaAuth";

    private readonly IHttpClientFactory _httpClientFactory;
    private readonly ILogger<ApiLoginService> _logger;
    private readonly string[] _baseUrls;

    public ApiLoginService(IHttpClientFactory httpClientFactory, ILogger<ApiLoginService> logger, ApiSettings settings)
    {
        _httpClientFactory = httpClientFactory;
        _logger = logger;
        _baseUrls = settings.BaseUrls;
    }

    public async Task<LoginUserModel?> LoginAsync(string userName, string password)
    {
        var login = userName?.Trim() ?? string.Empty;
        var clave = password?.Trim() ?? string.Empty;

        if (string.IsNullOrWhiteSpace(login))
            return null;

        foreach (var baseUrl in _baseUrls)
        {
            try
            {
                var http = _httpClientFactory.CreateClient(AuthClientName);

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
                            StoreId = result.US_ID_STORE ?? 0,
                            RoleCode = result.US_ROLE_CODE ?? string.Empty,
                            RoleName = result.US_ROLE_NAME ?? string.Empty,
                            SecurityLevel = result.US_SECURITY_LEVEL,
                            Privileges = result.US_PRIVILEGES,
                            RolePrivileges = result.US_ROLE_PRIVILEGES ?? string.Empty
                        };
                    }
                }
            }
            catch (HttpRequestException ex)
            {
                _logger.LogWarning(ex, "Error de conexión al autenticar desde {BaseUrl}", baseUrl);
                throw;
            }
            catch (TaskCanceledException ex)
            {
                _logger.LogWarning(ex, "Timeout al autenticar desde {BaseUrl}", baseUrl);
                throw;
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "Error al autenticar usuario desde {BaseUrl}", baseUrl);
            }
        }

        return null;
    }

    public async Task<bool> IsDatabaseConnectedAsync()
    {
        foreach (var baseUrl in _baseUrls)
        {
            try
            {
                var http = _httpClientFactory.CreateClient(AuthClientName);
                var url = $"{baseUrl}/api/StoreConfig";
                var response = await http.GetAsync(url);
                if (response.IsSuccessStatusCode)
                    return true;
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "Error al verificar conexión a BD desde {BaseUrl}", baseUrl);
            }
        }

        return false;
    }

    public async Task<LoginConnectionInfoModel?> GetConnectionInfoAsync()
    {
        foreach (var baseUrl in _baseUrls)
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
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "Error al obtener info de conexión desde {BaseUrl}", baseUrl);
            }
        }

        return new LoginConnectionInfoModel
        {
            ApiBaseUrl = _baseUrls[0],
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
        public string? US_ROLE_CODE { get; set; }
        public string? US_ROLE_NAME { get; set; }
        public short US_SECURITY_LEVEL { get; set; }
        public int US_PRIVILEGES { get; set; }
        public string? US_ROLE_PRIVILEGES { get; set; }
    }

    private sealed class ApiConnectionInfoResponse
    {
        public string? DatabaseServer { get; set; }
        public string? DatabaseName { get; set; }
    }
}
