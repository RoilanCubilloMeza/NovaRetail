using System.Net.Http.Json;
using Microsoft.Extensions.Logging;
using NovaRetail.Models;

namespace NovaRetail.Data;

public sealed class ApiStoreConfigService : IStoreConfigService
{
    private const string ClientName = "NovaStoreConfig";

    private readonly IHttpClientFactory _httpClientFactory;
    private readonly ILogger<ApiStoreConfigService> _logger;
    private readonly string[] _baseUrls;

    public ApiStoreConfigService(IHttpClientFactory httpClientFactory, ILogger<ApiStoreConfigService> logger, ApiSettings settings)
    {
        _httpClientFactory = httpClientFactory;
        _logger = logger;
        _baseUrls = settings.BaseUrls;
    }

    public async Task<StoreConfigModel> GetConfigAsync()
    {
        foreach (var baseUrl in _baseUrls)
        {
            try
            {
                var http = _httpClientFactory.CreateClient(ClientName);
                var result = await http.GetFromJsonAsync<StoreConfigModel>($"{baseUrl}/api/StoreConfig");
                if (result is not null)
                    return result;
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "Error al obtener configuración de tienda desde {BaseUrl}", baseUrl);
            }
        }

        return new StoreConfigModel();
    }

    public async Task<List<TenderModel>> GetTendersAsync()
    {
        foreach (var baseUrl in _baseUrls)
        {
            try
            {
                var http = _httpClientFactory.CreateClient(ClientName);
                var result = await http.GetFromJsonAsync<List<TenderModel>>($"{baseUrl}/api/StoreConfig/Tenders");
                if (result is not null && result.Count > 0)
                    return result;
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "Error al obtener tenders desde {BaseUrl}", baseUrl);
            }
        }

        return [];
    }

    public async Task<List<CategoryModel>> GetCategoriesAsync(string userName = null)
    {
        foreach (var baseUrl in _baseUrls)
        {
            try
            {
                var http = _httpClientFactory.CreateClient(ClientName);
                var url = string.IsNullOrWhiteSpace(userName)
                    ? $"{baseUrl}/api/Categories"
                    : $"{baseUrl}/api/Categories?userName={Uri.EscapeDataString(userName)}";
                var result = await http.GetFromJsonAsync<List<CategoryModel>>(url);
                if (result is not null && result.Count > 0)
                    return result;
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "Error al obtener categorías desde {BaseUrl}", baseUrl);
            }
        }

        return [];
    }

    public async Task<List<CategoryModel>> GetAllCategoriesAsync()
    {
        foreach (var baseUrl in _baseUrls)
        {
            try
            {
                var http = _httpClientFactory.CreateClient(ClientName);
                var result = await http.GetFromJsonAsync<List<CategoryModel>>($"{baseUrl}/api/Categories/All");
                if (result is not null && result.Count > 0)
                    return result;
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "Error al obtener todos los departamentos desde {BaseUrl}", baseUrl);
            }
        }

        return [];
    }

    public async Task<string> GetCategoryConfigAsync(string userName = null)
    {
        foreach (var baseUrl in _baseUrls)
        {
            try
            {
                var http = _httpClientFactory.CreateClient(ClientName);
                var url = string.IsNullOrWhiteSpace(userName)
                    ? $"{baseUrl}/api/Categories/Config"
                    : $"{baseUrl}/api/Categories/Config?userName={Uri.EscapeDataString(userName)}";
                var result = await http.GetFromJsonAsync<CategoryConfigResponse>(url);
                if (result is not null)
                    return result.SelectedIds ?? string.Empty;
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "Error al obtener configuración de categorías desde {BaseUrl}", baseUrl);
            }
        }

        return string.Empty;
    }

    public async Task<bool> SaveCategoryConfigAsync(string selectedIds, string userName = null)
    {
        foreach (var baseUrl in _baseUrls)
        {
            try
            {
                var http = _httpClientFactory.CreateClient(ClientName);
                var response = await http.PutAsJsonAsync(
                    $"{baseUrl}/api/Categories/Config",
                    new CategoryConfigResponse { SelectedIds = selectedIds, UserName = userName });
                return response.IsSuccessStatusCode;
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "Error al guardar configuración de categorías en {BaseUrl}", baseUrl);
            }
        }

        return false;
    }

    private sealed class CategoryConfigResponse
    {
        public string SelectedIds { get; set; } = string.Empty;
        public string UserName { get; set; }
    }
}
