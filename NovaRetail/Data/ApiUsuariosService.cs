using System.Text;
using System.Text.Json;
using Microsoft.Extensions.Logging;
using NovaRetail.Models;

namespace NovaRetail.Data;

public sealed class ApiUsuariosService : IUsuariosService
{
    private const string ClientName = "NovaStoreConfig";
    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web)
    {
        PropertyNameCaseInsensitive = true
    };

    private readonly IHttpClientFactory _httpClientFactory;
    private readonly ILogger<ApiUsuariosService> _logger;
    private readonly string[] _baseUrls;

    public ApiUsuariosService(IHttpClientFactory httpClientFactory, ILogger<ApiUsuariosService> logger, ApiSettings settings)
    {
        _httpClientFactory = httpClientFactory;
        _logger = logger;
        _baseUrls = settings.BaseUrls;
    }

    public async Task<List<UsuarioModel>> GetUsuariosAsync()
    {
        foreach (var baseUrl in _baseUrls)
        {
            try
            {
                var http = _httpClientFactory.CreateClient(ClientName);
                var response = await http.GetAsync($"{baseUrl}/api/Usuarios");
                if (!response.IsSuccessStatusCode)
                    continue;

                var json = await response.Content.ReadAsStringAsync();
                var result = JsonSerializer.Deserialize<List<UsuarioModel>>(json, JsonOptions);
                if (result is not null)
                    return result;
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "Error obteniendo usuarios desde {BaseUrl}", baseUrl);
            }
        }

        return new List<UsuarioModel>();
    }

    public async Task<List<RolModel>> GetRolesAsync()
    {
        foreach (var baseUrl in _baseUrls)
        {
            try
            {
                var http = _httpClientFactory.CreateClient(ClientName);
                var response = await http.GetAsync($"{baseUrl}/api/Usuarios/Roles");
                if (!response.IsSuccessStatusCode)
                    continue;

                var json = await response.Content.ReadAsStringAsync();
                var result = JsonSerializer.Deserialize<List<RolModel>>(json, JsonOptions);
                if (result is not null)
                    return result;
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "Error obteniendo roles desde {BaseUrl}", baseUrl);
            }
        }

        return new List<RolModel>();
    }

    public async Task<bool> SaveUsuarioAsync(int id, string nombreCompleto, short securityLevel, int roleId)
    {
        foreach (var baseUrl in _baseUrls)
        {
            try
            {
                var http = _httpClientFactory.CreateClient(ClientName);
                var payload = new { Id = id, NombreCompleto = nombreCompleto, SecurityLevel = securityLevel, RoleId = roleId };
                var json = new StringContent(JsonSerializer.Serialize(payload), Encoding.UTF8, "application/json");
                var response = await http.PutAsync($"{baseUrl}/api/Usuarios", json);
                return response.IsSuccessStatusCode;
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "Error guardando usuario desde {BaseUrl}", baseUrl);
            }
        }

        return false;
    }
}
