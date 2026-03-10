using System.Net.Http;
using System.Net.Http.Json;
using NovaRetail.Models;

namespace NovaRetail.Data
{
    public class ApiStoreConfigService : IStoreConfigService
    {
        private const string ClientName = "NovaStoreConfig";
        private static readonly string[] BaseUrls =
        {
            "http://localhost:52500",
            "http://127.0.0.1:52500"
        };

        private readonly IHttpClientFactory _httpClientFactory;

        public ApiStoreConfigService(IHttpClientFactory httpClientFactory)
        {
            _httpClientFactory = httpClientFactory;
        }

        public async Task<StoreConfigModel> GetConfigAsync()
        {
            foreach (var baseUrl in BaseUrls)
            {
                try
                {
                    var http = _httpClientFactory.CreateClient(ClientName);
                    var result = await http.GetFromJsonAsync<StoreConfigModel>($"{baseUrl}/api/StoreConfig");
                    if (result is not null)
                        return result;
                }
                catch
                {
                }
            }

            return new StoreConfigModel();
        }

        public async Task<List<TenderModel>> GetTendersAsync()
        {
            foreach (var baseUrl in BaseUrls)
            {
                try
                {
                    var http = _httpClientFactory.CreateClient(ClientName);
                    var result = await http.GetFromJsonAsync<List<TenderModel>>($"{baseUrl}/api/StoreConfig/Tenders");
                    if (result is not null && result.Count > 0)
                        return result;
                }
                catch
                {
                }
            }

            return [];
        }
    }
}
