using System.Net.Http.Json;
using System.Text;
using System.Text.Json;
using Microsoft.Extensions.Logging;
using NovaRetail.Models;

namespace NovaRetail.Data;

public sealed class ApiParametrosService : IParametrosService
{
    private const string ClientName = "NovaStoreConfig";
    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web)
    {
        PropertyNameCaseInsensitive = true
    };

    private readonly IHttpClientFactory _httpClientFactory;
    private readonly ILogger<ApiParametrosService> _logger;
    private readonly string[] _baseUrls;
    private TenderSettingsModel? _cachedTenderSettings;
    private DateTime _tenderSettingsCacheExpiry;
    private static readonly TimeSpan TenderSettingsCacheDuration = TimeSpan.FromMinutes(10);

    public ApiParametrosService(IHttpClientFactory httpClientFactory, ILogger<ApiParametrosService> logger, ApiSettings settings)
    {
        _httpClientFactory = httpClientFactory;
        _logger = logger;
        _baseUrls = settings.BaseUrls;
    }

    public async Task<List<ParametroModel>> GetParametrosAsync()
    {
        foreach (var baseUrl in _baseUrls)
        {
            try
            {
                var http = _httpClientFactory.CreateClient(ClientName);
                var response = await http.GetAsync($"{baseUrl}/api/Parametros");
                if (!response.IsSuccessStatusCode)
                    continue;

                var json = await response.Content.ReadAsStringAsync();
                var result = JsonSerializer.Deserialize<List<ParametroModel>>(json, JsonOptions);
                if (result is not null)
                    return result;
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "Error obteniendo parámetros desde {BaseUrl}", baseUrl);
            }
        }

        return new List<ParametroModel>();
    }

    public async Task<bool> SaveParametroAsync(ParametroModel parametro)
    {
        foreach (var baseUrl in _baseUrls)
        {
            try
            {
                var http = _httpClientFactory.CreateClient(ClientName);
                var json = new StringContent(JsonSerializer.Serialize(parametro), Encoding.UTF8, "application/json");
                var response = await http.PutAsync($"{baseUrl}/api/Parametros", json);
                return response.IsSuccessStatusCode;
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "Error guardando parámetro desde {BaseUrl}", baseUrl);
            }
        }

        return false;
    }

    public async Task<TenderSettingsModel?> GetTenderSettingsAsync()
    {
        if (_cachedTenderSettings is not null && DateTime.UtcNow < _tenderSettingsCacheExpiry)
            return _cachedTenderSettings;

        foreach (var baseUrl in _baseUrls)
        {
            try
            {
                var http = _httpClientFactory.CreateClient(ClientName);
                var response = await http.GetAsync($"{baseUrl}/api/Parametros/Tenders");
                if (!response.IsSuccessStatusCode)
                    continue;

                var json = await response.Content.ReadAsStringAsync();
                var result = JsonSerializer.Deserialize<TenderSettingsModel>(json, JsonOptions);
                if (result is not null)
                {
                    _cachedTenderSettings = result;
                    _tenderSettingsCacheExpiry = DateTime.UtcNow.Add(TenderSettingsCacheDuration);
                    return result;
                }
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "Error obteniendo tender settings desde {BaseUrl}", baseUrl);
            }
        }

        return null;
    }

    public async Task<bool> SaveTenderSettingsAsync(TenderSettingsModel settings)
    {
        foreach (var baseUrl in _baseUrls)
        {
            try
            {
                var http = _httpClientFactory.CreateClient(ClientName);
                var json = new StringContent(JsonSerializer.Serialize(settings), Encoding.UTF8, "application/json");
                var response = await http.PutAsync($"{baseUrl}/api/Parametros/Tenders", json);
                if (response.IsSuccessStatusCode)
                {
                    _cachedTenderSettings = settings;
                    _tenderSettingsCacheExpiry = DateTime.UtcNow.Add(TenderSettingsCacheDuration);
                }

                return response.IsSuccessStatusCode;
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "Error guardando tender settings desde {BaseUrl}", baseUrl);
            }
        }

        return false;
    }
}
