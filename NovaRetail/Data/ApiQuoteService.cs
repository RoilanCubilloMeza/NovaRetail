using System.Net.Http.Json;
using Newtonsoft.Json;
using NovaRetail.Models;

namespace NovaRetail.Data;

public class ApiQuoteService : IQuoteService
{
    private const string QuoteClientName = "NovaQuotes";
    private static readonly string[] BaseUrls =
    {
        "http://localhost:52500",
        "http://127.0.0.1:52500"
    };

    private readonly IHttpClientFactory _httpClientFactory;

    public ApiQuoteService(IHttpClientFactory httpClientFactory)
    {
        _httpClientFactory = httpClientFactory;
    }

    public async Task<NovaRetailCreateQuoteResponse> CreateQuoteAsync(NovaRetailCreateQuoteRequest request, CancellationToken cancellationToken = default)
    {
        string? lastErrorMessage = null;

        foreach (var baseUrl in BaseUrls)
        {
            try
            {
                var http = _httpClientFactory.CreateClient(QuoteClientName);
                using var response = await http.PostAsJsonAsync($"{baseUrl}/api/NovaRetailSales/create-quote", request, cancellationToken);
                var content = await response.Content.ReadAsStringAsync(cancellationToken);

                if (!string.IsNullOrWhiteSpace(content))
                {
                    var payload = JsonConvert.DeserializeObject<NovaRetailCreateQuoteResponse>(content);
                    if (payload is not null)
                    {
                        if (string.IsNullOrWhiteSpace(payload.Message))
                            payload.Message = response.ReasonPhrase ?? "Respuesta recibida.";

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

        return new NovaRetailCreateQuoteResponse
        {
            Ok = false,
            Message = string.IsNullOrWhiteSpace(lastErrorMessage)
                ? "No fue posible comunicarse con el servicio."
                : lastErrorMessage
        };
    }

    public async Task<NovaRetailCreateQuoteResponse> UpdateQuoteAsync(NovaRetailCreateQuoteRequest request, CancellationToken cancellationToken = default)
    {
        string? lastErrorMessage = null;

        foreach (var baseUrl in BaseUrls)
        {
            try
            {
                var http = _httpClientFactory.CreateClient(QuoteClientName);
                using var response = await http.PostAsJsonAsync($"{baseUrl}/api/NovaRetailSales/update-quote", request, cancellationToken);
                var content = await response.Content.ReadAsStringAsync(cancellationToken);

                if (!string.IsNullOrWhiteSpace(content))
                {
                    var payload = JsonConvert.DeserializeObject<NovaRetailCreateQuoteResponse>(content);
                    if (payload is not null)
                    {
                        if (string.IsNullOrWhiteSpace(payload.Message))
                            payload.Message = response.ReasonPhrase ?? "Respuesta recibida.";

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

        return new NovaRetailCreateQuoteResponse
        {
            Ok = false,
            Message = string.IsNullOrWhiteSpace(lastErrorMessage)
                ? "No fue posible comunicarse con el servicio."
                : lastErrorMessage
        };
    }

    public async Task<NovaRetailListOrdersResponse> ListOrdersAsync(int storeId, int type, string search = "", CancellationToken cancellationToken = default)
    {
        string? lastErrorMessage = null;

        foreach (var baseUrl in BaseUrls)
        {
            try
            {
                var http = _httpClientFactory.CreateClient(QuoteClientName);
                var url = $"{baseUrl}/api/NovaRetailSales/list-orders?storeId={storeId}&type={type}&search={Uri.EscapeDataString(search ?? string.Empty)}";
                var content = await http.GetStringAsync(url, cancellationToken);

                if (!string.IsNullOrWhiteSpace(content))
                {
                    var payload = JsonConvert.DeserializeObject<NovaRetailListOrdersResponse>(content);
                    if (payload is not null)
                        return payload;

                    lastErrorMessage = content;
                }
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

        return new NovaRetailListOrdersResponse
        {
            Ok = false,
            Message = string.IsNullOrWhiteSpace(lastErrorMessage)
                ? "No fue posible comunicarse con el servicio."
                : lastErrorMessage
        };
    }

    public async Task<NovaRetailOrderDetailResponse> GetOrderDetailAsync(int orderId, CancellationToken cancellationToken = default)
    {
        string? lastErrorMessage = null;

        foreach (var baseUrl in BaseUrls)
        {
            try
            {
                var http = _httpClientFactory.CreateClient(QuoteClientName);
                var content = await http.GetStringAsync($"{baseUrl}/api/NovaRetailSales/order-detail/{orderId}", cancellationToken);

                if (!string.IsNullOrWhiteSpace(content))
                {
                    var payload = JsonConvert.DeserializeObject<NovaRetailOrderDetailResponse>(content);
                    if (payload is not null)
                        return payload;

                    lastErrorMessage = content;
                }
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

        return new NovaRetailOrderDetailResponse
        {
            Ok = false,
            Message = string.IsNullOrWhiteSpace(lastErrorMessage)
                ? "No fue posible comunicarse con el servicio."
                : lastErrorMessage
        };
    }

    public async Task<NovaRetailCreateQuoteResponse> SaveHoldAsync(NovaRetailCreateQuoteRequest request, CancellationToken cancellationToken = default)
    {
        string? lastErrorMessage = null;

        foreach (var baseUrl in BaseUrls)
        {
            try
            {
                var http = _httpClientFactory.CreateClient(QuoteClientName);
                using var response = await http.PostAsJsonAsync($"{baseUrl}/api/NovaRetailSales/save-hold", request, cancellationToken);
                var content = await response.Content.ReadAsStringAsync(cancellationToken);

                if (!string.IsNullOrWhiteSpace(content))
                {
                    var payload = JsonConvert.DeserializeObject<NovaRetailCreateQuoteResponse>(content);
                    if (payload is not null)
                    {
                        if (string.IsNullOrWhiteSpace(payload.Message))
                            payload.Message = response.ReasonPhrase ?? "Respuesta recibida.";
                        return payload;
                    }
                    lastErrorMessage = content;
                }
                if (!response.IsSuccessStatusCode)
                    lastErrorMessage = response.ReasonPhrase ?? $"Error HTTP {(int)response.StatusCode}.";
            }
            catch (OperationCanceledException) { throw; }
            catch (Exception ex) { lastErrorMessage = ex.Message; }
        }

        return new NovaRetailCreateQuoteResponse
        {
            Ok = false,
            Message = string.IsNullOrWhiteSpace(lastErrorMessage) ? "No fue posible comunicarse con el servicio." : lastErrorMessage
        };
    }

    public async Task<NovaRetailCreateQuoteResponse> UpdateHoldAsync(NovaRetailCreateQuoteRequest request, CancellationToken cancellationToken = default)
    {
        string? lastErrorMessage = null;

        foreach (var baseUrl in BaseUrls)
        {
            try
            {
                var http = _httpClientFactory.CreateClient(QuoteClientName);
                using var response = await http.PostAsJsonAsync($"{baseUrl}/api/NovaRetailSales/update-hold", request, cancellationToken);
                var content = await response.Content.ReadAsStringAsync(cancellationToken);

                if (!string.IsNullOrWhiteSpace(content))
                {
                    var payload = JsonConvert.DeserializeObject<NovaRetailCreateQuoteResponse>(content);
                    if (payload is not null)
                    {
                        if (string.IsNullOrWhiteSpace(payload.Message))
                            payload.Message = response.ReasonPhrase ?? "Respuesta recibida.";
                        return payload;
                    }
                    lastErrorMessage = content;
                }
                if (!response.IsSuccessStatusCode)
                    lastErrorMessage = response.ReasonPhrase ?? $"Error HTTP {(int)response.StatusCode}.";
            }
            catch (OperationCanceledException) { throw; }
            catch (Exception ex) { lastErrorMessage = ex.Message; }
        }

        return new NovaRetailCreateQuoteResponse
        {
            Ok = false,
            Message = string.IsNullOrWhiteSpace(lastErrorMessage) ? "No fue posible comunicarse con el servicio." : lastErrorMessage
        };
    }

    public async Task<NovaRetailListOrdersResponse> ListHoldsAsync(int storeId, string search = "", CancellationToken cancellationToken = default)
    {
        string? lastErrorMessage = null;

        foreach (var baseUrl in BaseUrls)
        {
            try
            {
                var http = _httpClientFactory.CreateClient(QuoteClientName);
                var url = $"{baseUrl}/api/NovaRetailSales/list-holds?storeId={storeId}&search={Uri.EscapeDataString(search ?? string.Empty)}";
                var content = await http.GetStringAsync(url, cancellationToken);

                if (!string.IsNullOrWhiteSpace(content))
                {
                    var payload = JsonConvert.DeserializeObject<NovaRetailListOrdersResponse>(content);
                    if (payload is not null)
                        return payload;
                    lastErrorMessage = content;
                }
            }
            catch (OperationCanceledException) { throw; }
            catch (Exception ex) { lastErrorMessage = ex.Message; }
        }

        return new NovaRetailListOrdersResponse
        {
            Ok = false,
            Message = string.IsNullOrWhiteSpace(lastErrorMessage) ? "No fue posible comunicarse con el servicio." : lastErrorMessage
        };
    }

    public async Task<NovaRetailOrderDetailResponse> GetHoldDetailAsync(int holdId, CancellationToken cancellationToken = default)
    {
        string? lastErrorMessage = null;

        foreach (var baseUrl in BaseUrls)
        {
            try
            {
                var http = _httpClientFactory.CreateClient(QuoteClientName);
                var content = await http.GetStringAsync($"{baseUrl}/api/NovaRetailSales/hold-detail/{holdId}", cancellationToken);

                if (!string.IsNullOrWhiteSpace(content))
                {
                    var payload = JsonConvert.DeserializeObject<NovaRetailOrderDetailResponse>(content);
                    if (payload is not null)
                        return payload;
                    lastErrorMessage = content;
                }
            }
            catch (OperationCanceledException) { throw; }
            catch (Exception ex) { lastErrorMessage = ex.Message; }
        }

        return new NovaRetailOrderDetailResponse
        {
            Ok = false,
            Message = string.IsNullOrWhiteSpace(lastErrorMessage) ? "No fue posible comunicarse con el servicio." : lastErrorMessage
        };
    }

    public async Task<NovaRetailCreateQuoteResponse> DeleteQuoteAsync(int orderId, CancellationToken cancellationToken = default)
    {
        string? lastErrorMessage = null;

        foreach (var baseUrl in BaseUrls)
        {
            try
            {
                var http = _httpClientFactory.CreateClient(QuoteClientName);
                using var response = await http.DeleteAsync($"{baseUrl}/api/NovaRetailSales/delete-quote/{orderId}", cancellationToken);
                var content = await response.Content.ReadAsStringAsync(cancellationToken);

                if (!string.IsNullOrWhiteSpace(content))
                {
                    var payload = JsonConvert.DeserializeObject<NovaRetailCreateQuoteResponse>(content);
                    if (payload is not null)
                        return payload;
                    lastErrorMessage = content;
                }
                if (!response.IsSuccessStatusCode)
                    lastErrorMessage = response.ReasonPhrase ?? $"Error HTTP {(int)response.StatusCode}.";
            }
            catch (OperationCanceledException) { throw; }
            catch (Exception ex) { lastErrorMessage = ex.Message; }
        }

        return new NovaRetailCreateQuoteResponse
        {
            Ok = false,
            Message = string.IsNullOrWhiteSpace(lastErrorMessage) ? "No fue posible comunicarse con el servicio." : lastErrorMessage
        };
    }

    public async Task<NovaRetailCreateQuoteResponse> DeleteHoldAsync(int holdId, CancellationToken cancellationToken = default)
    {
        string? lastErrorMessage = null;

        foreach (var baseUrl in BaseUrls)
        {
            try
            {
                var http = _httpClientFactory.CreateClient(QuoteClientName);
                using var response = await http.DeleteAsync($"{baseUrl}/api/NovaRetailSales/delete-hold/{holdId}", cancellationToken);
                var content = await response.Content.ReadAsStringAsync(cancellationToken);

                if (!string.IsNullOrWhiteSpace(content))
                {
                    var payload = JsonConvert.DeserializeObject<NovaRetailCreateQuoteResponse>(content);
                    if (payload is not null)
                        return payload;
                    lastErrorMessage = content;
                }
                if (!response.IsSuccessStatusCode)
                    lastErrorMessage = response.ReasonPhrase ?? $"Error HTTP {(int)response.StatusCode}.";
            }
            catch (OperationCanceledException) { throw; }
            catch (Exception ex) { lastErrorMessage = ex.Message; }
        }

        return new NovaRetailCreateQuoteResponse
        {
            Ok = false,
            Message = string.IsNullOrWhiteSpace(lastErrorMessage) ? "No fue posible comunicarse con el servicio." : lastErrorMessage
        };
    }
}
