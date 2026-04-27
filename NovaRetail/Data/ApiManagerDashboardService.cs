using Microsoft.Extensions.Logging;
using Newtonsoft.Json;
using NovaRetail.Models;

namespace NovaRetail.Data;

public sealed class ApiManagerDashboardService : IManagerDashboardService
{
    private const string ClientName = "NovaManager";
    private readonly IHttpClientFactory _httpClientFactory;
    private readonly ILogger<ApiManagerDashboardService> _logger;
    private readonly string[] _baseUrls;

    public ApiManagerDashboardService(IHttpClientFactory httpClientFactory, ILogger<ApiManagerDashboardService> logger, ApiSettings settings)
    {
        _httpClientFactory = httpClientFactory;
        _logger = logger;
        _baseUrls = settings.BaseUrls;
    }

    public async Task<ManagerDashboardResponse> GetDashboardAsync(int storeId, DateTime date, CancellationToken cancellationToken = default)
        => await GetAsync<ManagerDashboardResponse>(
            $"api/NovaRetailManager/dashboard?storeId={storeId}&date={Uri.EscapeDataString(date.ToString("yyyy-MM-dd"))}",
            "dashboard",
            () => new ManagerDashboardResponse(),
            cancellationToken);

    public async Task<ManagerActionLogResponse> GetActivityLogAsync(int storeId, DateTime date, string search = "", int top = 100, CancellationToken cancellationToken = default)
        => await GetAsync<ManagerActionLogResponse>(
            $"api/NovaRetailManager/activity-log?storeId={storeId}&top={top}&date={Uri.EscapeDataString(date.ToString("yyyy-MM-dd"))}&search={Uri.EscapeDataString(search ?? string.Empty)}",
            "historial de acciones",
            () => new ManagerActionLogResponse(),
            cancellationToken);

    private async Task<T> GetAsync<T>(string path, string label, Func<T> fallbackFactory, CancellationToken cancellationToken)
    {
        string? lastError = null;
        foreach (var baseUrl in _baseUrls)
        {
            try
            {
                var http = _httpClientFactory.CreateClient(ClientName);
                var separator = path.Contains('?') ? "&" : "?";
                var url = $"{baseUrl.TrimEnd('/')}/{path}{separator}_ts={DateTimeOffset.UtcNow.ToUnixTimeMilliseconds()}";
                var content = await http.GetStringAsync(url, cancellationToken);
                if (!string.IsNullOrWhiteSpace(content))
                {
                    var payload = JsonConvert.DeserializeObject<T>(content);
                    if (payload is not null)
                        return payload;

                    lastError = content;
                }
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "Error al cargar {Label} desde {BaseUrl}", label, baseUrl);
                lastError = ex.Message;
            }
        }

        var fallback = fallbackFactory();
        typeof(T).GetProperty("Ok")?.SetValue(fallback, false);
        typeof(T).GetProperty("Message")?.SetValue(fallback, string.IsNullOrWhiteSpace(lastError) ? "No fue posible comunicarse con el servicio." : lastError);
        return fallback;
    }
}
