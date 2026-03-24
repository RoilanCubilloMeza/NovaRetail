using System.Net.Http;
using System.Net.Http.Json;
using Microsoft.Extensions.Logging;
using NovaRetail.Models;

namespace NovaRetail.Data
{
    public class ApiStoreConfigService : IStoreConfigService
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
    }
}
