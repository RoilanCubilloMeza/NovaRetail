using System.Net.Http.Json;
using Microsoft.Extensions.Logging;
using Newtonsoft.Json;
using NovaRetail.Models;

namespace NovaRetail.Data;

public class ApiSaleService : ISaleService
{
    private const string SalesClientName = "NovaSales";

    private readonly IHttpClientFactory _httpClientFactory;
    private readonly ILogger<ApiSaleService> _logger;
    private readonly string[] _baseUrls;

    public ApiSaleService(IHttpClientFactory httpClientFactory, ILogger<ApiSaleService> logger, ApiSettings settings)
    {
        _httpClientFactory = httpClientFactory;
        _logger = logger;
        _baseUrls = settings.BaseUrls;
    }

    public async Task<NovaRetailCreateSaleResponse> CreateSaleAsync(NovaRetailCreateSaleRequest request, CancellationToken cancellationToken = default)
    {
        string? lastErrorMessage = null;

        foreach (var baseUrl in _baseUrls)
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
                _logger.LogWarning(ex, "Error al crear venta en {BaseUrl}", baseUrl);
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
