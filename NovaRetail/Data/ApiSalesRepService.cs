using System.Net.Http.Json;
using Microsoft.Extensions.Logging;
using NovaRetail.Models;

namespace NovaRetail.Data
{
    public class ApiSalesRepService : ISalesRepService
    {
        private const string ClientName = "NovaSalesRep";

        private readonly IHttpClientFactory _httpClientFactory;
        private readonly ILogger<ApiSalesRepService> _logger;
        private readonly string[] _baseUrls;

        public ApiSalesRepService(IHttpClientFactory httpClientFactory, ILogger<ApiSalesRepService> logger, ApiSettings settings)
        {
            _httpClientFactory = httpClientFactory;
            _logger = logger;
            _baseUrls = settings.BaseUrls;
        }

        public async Task<List<SalesRepModel>> GetAllAsync()
        {
            foreach (var baseUrl in _baseUrls)
            {
                try
                {
                    var http = _httpClientFactory.CreateClient(ClientName);
                    var result = await http.GetFromJsonAsync<List<SalesRepModel>>($"{baseUrl}/api/SalesRep/Get");
                    if (result is not null && result.Count > 0)
                        return result;
                }
                catch (Exception ex)
                {
                    _logger.LogWarning(ex, "Error al obtener vendedores desde {BaseUrl}", baseUrl);
                }
            }

            return [];
        }
    }
}
