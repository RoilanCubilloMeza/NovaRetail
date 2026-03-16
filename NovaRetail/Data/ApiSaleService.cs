using System.Net.Http.Json;
using Newtonsoft.Json;
using NovaRetail.Models;

namespace NovaRetail.Data;

public class ApiSaleService : ISaleService
{
    private const string SalesClientName = "NovaSales";
    private static readonly string[] BaseUrls =
    {
        "http://localhost:52500",
        "http://127.0.0.1:52500"
    };

    private readonly IHttpClientFactory _httpClientFactory;

    public ApiSaleService(IHttpClientFactory httpClientFactory)
    {
        _httpClientFactory = httpClientFactory;
    }

    public async Task<NovaRetailCreateSaleResponse> CreateSaleAsync(NovaRetailCreateSaleRequest request, CancellationToken cancellationToken = default)
    {
        string? lastErrorMessage = null;

        foreach (var baseUrl in BaseUrls)
        {
            try
            {
                var http = _httpClientFactory.CreateClient(SalesClientName);
                using var response = await http.PostAsJsonAsync($"{baseUrl}/api/NovaRetailSales/create-sale", request, cancellationToken);
                var content = await response.Content.ReadAsStringAsync(cancellationToken);

                if (!string.IsNullOrWhiteSpace(content))
                {
                    var payload = JsonConvert.DeserializeObject<NovaRetailCreateSaleResponse>(content);
                    if (payload is not null)
                    {
                        if (string.IsNullOrWhiteSpace(payload.Message))
                            payload.Message = response.ReasonPhrase ?? "Respuesta recibida del servicio de ventas.";

                        return payload;
                    }

                    lastErrorMessage = content;
                }

                if (!response.IsSuccessStatusCode)
                    lastErrorMessage = response.ReasonPhrase ?? $"Error HTTP {(int)response.StatusCode}.";
            }
            catch (OperationCanceledException)
            {
                throw;
            }
            catch (Exception ex)
            {
                lastErrorMessage = ex.Message;
            }
        }

        return new NovaRetailCreateSaleResponse
        {
            Ok = false,
            Message = string.IsNullOrWhiteSpace(lastErrorMessage)
                ? "No fue posible comunicarse con el servicio de ventas."
                : lastErrorMessage
        };
    }
}
