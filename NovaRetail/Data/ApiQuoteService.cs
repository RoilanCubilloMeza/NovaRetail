using System.Net.Http.Json;
using Microsoft.Extensions.Logging;
using Newtonsoft.Json;
using NovaRetail.Models;

namespace NovaRetail.Data;

public sealed class ApiQuoteService : IQuoteService
{
    private const string QuoteClientName = "NovaQuotes";

    private readonly IHttpClientFactory _httpClientFactory;
    private readonly ILogger<ApiQuoteService> _logger;
    private readonly string[] _baseUrls;

    public ApiQuoteService(IHttpClientFactory httpClientFactory, ILogger<ApiQuoteService> logger, ApiSettings settings)
    {
        _httpClientFactory = httpClientFactory;
        _logger = logger;
        _baseUrls = settings.BaseUrls;
    }

    private static string AppendNoCacheToken(string url)
    {
        var separator = url.Contains('?') ? "&" : "?";
        return $"{url}{separator}_ts={DateTimeOffset.UtcNow.ToUnixTimeMilliseconds()}";
    }

    public async Task<NovaRetailCreateQuoteResponse> CreateQuoteAsync(NovaRetailCreateQuoteRequest request, CancellationToken cancellationToken = default)
    {
        string? lastErrorMessage = null;

        foreach (var baseUrl in _baseUrls)
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
                _logger.LogWarning(ex, "Error al crear cotización en {BaseUrl}", baseUrl);
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

        foreach (var baseUrl in _baseUrls)
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
                _logger.LogWarning(ex, "Error al actualizar cotización en {BaseUrl}", baseUrl);
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

    public async Task<NovaRetailCreateQuoteResponse> CreateWorkOrderAsync(NovaRetailCreateQuoteRequest request, CancellationToken cancellationToken = default)
    {
        string? lastErrorMessage = null;

        foreach (var baseUrl in _baseUrls)
        {
            try
            {
                var http = _httpClientFactory.CreateClient(QuoteClientName);
                using var response = await http.PostAsJsonAsync($"{baseUrl}/api/NovaRetailSales/create-work-order", request, cancellationToken);
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
                _logger.LogWarning(ex, "Error al crear orden de trabajo en {BaseUrl}", baseUrl);
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

    public async Task<NovaRetailCreateQuoteResponse> UpdateWorkOrderAsync(NovaRetailCreateQuoteRequest request, CancellationToken cancellationToken = default)
    {
        string? lastErrorMessage = null;

        foreach (var baseUrl in _baseUrls)
        {
            try
            {
                var http = _httpClientFactory.CreateClient(QuoteClientName);
                using var response = await http.PostAsJsonAsync($"{baseUrl}/api/NovaRetailSales/update-work-order", request, cancellationToken);
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
                _logger.LogWarning(ex, "Error al actualizar orden de trabajo en {BaseUrl}", baseUrl);
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

        foreach (var baseUrl in _baseUrls)
        {
            try
            {
                var http = _httpClientFactory.CreateClient(QuoteClientName);
                var url = AppendNoCacheToken($"{baseUrl}/api/NovaRetailSales/list-orders?storeId={storeId}&type={type}&search={Uri.EscapeDataString(search ?? string.Empty)}");
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
                _logger.LogWarning(ex, "Error al listar órdenes desde {BaseUrl}", baseUrl);
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

        foreach (var baseUrl in _baseUrls)
        {
            try
            {
                var http = _httpClientFactory.CreateClient(QuoteClientName);
                var content = await http.GetStringAsync(AppendNoCacheToken($"{baseUrl}/api/NovaRetailSales/order-detail/{orderId}"), cancellationToken);

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
                _logger.LogWarning(ex, "Error al obtener detalle de orden desde {BaseUrl}", baseUrl);
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

        foreach (var baseUrl in _baseUrls)
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
            catch (Exception ex) { _logger.LogWarning(ex, "Error al guardar hold en {BaseUrl}", baseUrl); lastErrorMessage = ex.Message; }
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

        foreach (var baseUrl in _baseUrls)
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
            catch (Exception ex) { _logger.LogWarning(ex, "Error al actualizar hold en {BaseUrl}", baseUrl); lastErrorMessage = ex.Message; }
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

        foreach (var baseUrl in _baseUrls)
        {
            try
            {
                var http = _httpClientFactory.CreateClient(QuoteClientName);
                var url = AppendNoCacheToken($"{baseUrl}/api/NovaRetailSales/list-holds?storeId={storeId}&search={Uri.EscapeDataString(search ?? string.Empty)}");
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
            catch (Exception ex) { _logger.LogWarning(ex, "Error al listar holds desde {BaseUrl}", baseUrl); lastErrorMessage = ex.Message; }
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

        foreach (var baseUrl in _baseUrls)
        {
            try
            {
                var http = _httpClientFactory.CreateClient(QuoteClientName);
                var content = await http.GetStringAsync(AppendNoCacheToken($"{baseUrl}/api/NovaRetailSales/hold-detail/{holdId}"), cancellationToken);

                if (!string.IsNullOrWhiteSpace(content))
                {
                    var payload = JsonConvert.DeserializeObject<NovaRetailOrderDetailResponse>(content);
                    if (payload is not null)
                        return payload;
                    lastErrorMessage = content;
                }
            }
            catch (OperationCanceledException) { throw; }
            catch (Exception ex) { _logger.LogWarning(ex, "Error al obtener detalle de hold desde {BaseUrl}", baseUrl); lastErrorMessage = ex.Message; }
        }

        return new NovaRetailOrderDetailResponse
        {
            Ok = false,
            Message = string.IsNullOrWhiteSpace(lastErrorMessage) ? "No fue posible comunicarse con el servicio." : lastErrorMessage
        };
    }

    public async Task<NovaRetailCreateQuoteResponse> DeleteQuoteAsync(int orderId, int cashierId = 0, CancellationToken cancellationToken = default)
    {
        string? lastErrorMessage = null;

        foreach (var baseUrl in _baseUrls)
        {
            try
            {
                var http = _httpClientFactory.CreateClient(QuoteClientName);
                using var response = await http.DeleteAsync($"{baseUrl}/api/NovaRetailSales/delete-quote/{orderId}?cashierId={cashierId}", cancellationToken);
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
            catch (Exception ex) { _logger.LogWarning(ex, "Error al eliminar cotización en {BaseUrl}", baseUrl); lastErrorMessage = ex.Message; }
        }

        return new NovaRetailCreateQuoteResponse
        {
            Ok = false,
            Message = string.IsNullOrWhiteSpace(lastErrorMessage) ? "No fue posible comunicarse con el servicio." : lastErrorMessage
        };
    }

    public async Task<NovaRetailCreateQuoteResponse> DeleteWorkOrderAsync(int orderId, int cashierId = 0, CancellationToken cancellationToken = default)
    {
        string? lastErrorMessage = null;

        foreach (var baseUrl in _baseUrls)
        {
            try
            {
                var http = _httpClientFactory.CreateClient(QuoteClientName);
                using var response = await http.DeleteAsync($"{baseUrl}/api/NovaRetailSales/delete-work-order/{orderId}?cashierId={cashierId}", cancellationToken);
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
            catch (Exception ex) { _logger.LogWarning(ex, "Error al cancelar orden de trabajo en {BaseUrl}", baseUrl); lastErrorMessage = ex.Message; }
        }

        return new NovaRetailCreateQuoteResponse
        {
            Ok = false,
            Message = string.IsNullOrWhiteSpace(lastErrorMessage) ? "No fue posible comunicarse con el servicio." : lastErrorMessage
        };
    }

    public async Task<NovaRetailCreateQuoteResponse> DeleteHoldAsync(int holdId, int cashierId = 0, CancellationToken cancellationToken = default)
    {
        string? lastErrorMessage = null;

        foreach (var baseUrl in _baseUrls)
        {
            try
            {
                var http = _httpClientFactory.CreateClient(QuoteClientName);
                using var response = await http.DeleteAsync($"{baseUrl}/api/NovaRetailSales/delete-hold/{holdId}?cashierId={cashierId}", cancellationToken);
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
            catch (Exception ex) { _logger.LogWarning(ex, "Error al eliminar hold en {BaseUrl}", baseUrl); lastErrorMessage = ex.Message; }
        }

        return new NovaRetailCreateQuoteResponse
        {
            Ok = false,
            Message = string.IsNullOrWhiteSpace(lastErrorMessage) ? "No fue posible comunicarse con el servicio." : lastErrorMessage
        };
    }
}
