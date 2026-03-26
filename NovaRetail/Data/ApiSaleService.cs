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

    public async Task<NovaRetailInvoiceHistorySearchResponse> SearchInvoiceHistoryAsync(string search, CancellationToken cancellationToken = default)
    {
        string? lastErrorMessage = null;

        foreach (var baseUrl in _baseUrls)
        {
            try
            {
                var http = _httpClientFactory.CreateClient(SalesClientName);
                var top = 200;
                var url = $"{baseUrl}/api/NovaRetailSales/invoice-history?search={Uri.EscapeDataString(search ?? string.Empty)}&top={top}";
                using var response = await http.GetAsync(url, cancellationToken);
                var content = await response.Content.ReadAsStringAsync(cancellationToken);
                var trimmedContent = content?.TrimStart();

                if (!string.IsNullOrWhiteSpace(content) && (trimmedContent!.StartsWith("{") || trimmedContent.StartsWith("[")))
                {
                    var payload = JsonConvert.DeserializeObject<NovaRetailInvoiceHistorySearchResponse>(content);
                    if (payload is not null)
                    {
                        if (payload.Ok)
                            return payload;

                        lastErrorMessage = payload.Message;
                        continue;
                    }

                    lastErrorMessage = content;
                }
                else if (!string.IsNullOrWhiteSpace(content))
                {
                    lastErrorMessage = response.ReasonPhrase ?? $"Error HTTP {(int)response.StatusCode}.";
                }

                if (!response.IsSuccessStatusCode && string.IsNullOrWhiteSpace(lastErrorMessage))
                    lastErrorMessage = response.ReasonPhrase ?? $"Error HTTP {(int)response.StatusCode}.";
            }
            catch (OperationCanceledException)
            {
                throw;
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "Error al consultar historial de facturas en {BaseUrl}", baseUrl);
                lastErrorMessage = ex.Message;
            }
        }

        return new NovaRetailInvoiceHistorySearchResponse
        {
            Ok = false,
            Message = string.IsNullOrWhiteSpace(lastErrorMessage)
                ? "No fue posible comunicarse con el servicio de historial."
                : lastErrorMessage
        };
    }

    public async Task<NovaRetailInvoiceHistoryDetailResponse> GetInvoiceHistoryDetailAsync(int transactionNumber, CancellationToken cancellationToken = default)
    {
        string? lastErrorMessage = null;

        foreach (var baseUrl in _baseUrls)
        {
            try
            {
                var http = _httpClientFactory.CreateClient(SalesClientName);
                using var response = await http.GetAsync($"{baseUrl}/api/NovaRetailSales/invoice-history-detail/{transactionNumber}", cancellationToken);
                var content = await response.Content.ReadAsStringAsync(cancellationToken);
                var trimmedContent = content?.TrimStart();

                if (!string.IsNullOrWhiteSpace(content) && (trimmedContent!.StartsWith("{") || trimmedContent.StartsWith("[")))
                {
                    var payload = JsonConvert.DeserializeObject<NovaRetailInvoiceHistoryDetailResponse>(content);
                    if (payload is not null)
                    {
                        if (payload.Ok)
                            return payload;

                        lastErrorMessage = payload.Message;
                        continue;
                    }

                    lastErrorMessage = content;
                }
                else if (!string.IsNullOrWhiteSpace(content))
                {
                    lastErrorMessage = response.ReasonPhrase ?? $"Error HTTP {(int)response.StatusCode}.";
                }

                if (!response.IsSuccessStatusCode && string.IsNullOrWhiteSpace(lastErrorMessage))
                    lastErrorMessage = response.ReasonPhrase ?? $"Error HTTP {(int)response.StatusCode}.";
            }
            catch (OperationCanceledException)
            {
                throw;
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "Error al consultar detalle de factura {TransactionNumber} en {BaseUrl}", transactionNumber, baseUrl);
                lastErrorMessage = ex.Message;
            }
        }

        return new NovaRetailInvoiceHistoryDetailResponse
        {
            Ok = false,
            Message = string.IsNullOrWhiteSpace(lastErrorMessage)
                ? "No fue posible comunicarse con el servicio de historial."
                : lastErrorMessage
        };
    }
}
