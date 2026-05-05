using System.Net.Http.Json;
using System.Globalization;
using System.Text.Json.Serialization;
using Microsoft.Extensions.Logging;
using Microsoft.Maui.Storage;
using NovaRetail.Models;

namespace NovaRetail.Data;

public sealed class HaciendaExchangeRateService : IExchangeRateService
{
    private const string ClientName = "HaciendaExchangeRate";
    private const string DollarExchangeRateUrl = "https://api.hacienda.go.cr/indicadores/tc/dolar";
    private const string CacheDateKey = "HaciendaExchangeRate.CacheDate";
    private const string SaleRateKey = "HaciendaExchangeRate.SaleRate";
    private const string PurchaseRateKey = "HaciendaExchangeRate.PurchaseRate";
    private const string RateDateKey = "HaciendaExchangeRate.RateDate";

    private readonly IHttpClientFactory _httpClientFactory;
    private readonly ILogger<HaciendaExchangeRateService> _logger;
    private ExchangeRateModel? _cachedRate;
    private string _cachedLocalDate = string.Empty;

    public HaciendaExchangeRateService(
        IHttpClientFactory httpClientFactory,
        ILogger<HaciendaExchangeRateService> logger)
    {
        _httpClientFactory = httpClientFactory;
        _logger = logger;
    }

    public async Task<ExchangeRateModel?> GetDollarExchangeRateAsync(
        bool forceRefresh = false,
        CancellationToken cancellationToken = default)
    {
        if (TryGetTodayCachedRate(out var cachedRate))
            return cachedRate;

        try
        {
            var http = _httpClientFactory.CreateClient(ClientName);
            var response = await http.GetFromJsonAsync<HaciendaDollarExchangeRateResponse>(
                DollarExchangeRateUrl,
                cancellationToken);

            if (response?.Venta?.Valor is not > 0)
                return _cachedRate;

            var rate = new ExchangeRateModel
            {
                SaleRate = response.Venta.Valor,
                PurchaseRate = response.Compra?.Valor ?? 0m,
                RateDate = response.Venta.Fecha ?? response.Compra?.Fecha ?? DateTime.Today
            };

            SaveTodayCachedRate(rate);
            return rate;
        }
        catch (OperationCanceledException)
        {
            throw;
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Error al consultar tipo de cambio en Hacienda");
            return _cachedRate;
        }
    }

    private bool TryGetTodayCachedRate(out ExchangeRateModel? rate)
    {
        var todayKey = GetLocalDateKey(DateTime.Today);

        if (_cachedRate is not null && _cachedLocalDate == todayKey)
        {
            rate = _cachedRate;
            return true;
        }

        rate = null;
        if (Preferences.Default.Get(CacheDateKey, string.Empty) != todayKey)
            return false;

        var saleRateText = Preferences.Default.Get(SaleRateKey, string.Empty);
        if (!decimal.TryParse(saleRateText, NumberStyles.Number, CultureInfo.InvariantCulture, out var saleRate)
            || saleRate <= 0)
        {
            return false;
        }

        var purchaseRateText = Preferences.Default.Get(PurchaseRateKey, string.Empty);
        decimal.TryParse(purchaseRateText, NumberStyles.Number, CultureInfo.InvariantCulture, out var purchaseRate);

        var rateDateText = Preferences.Default.Get(RateDateKey, string.Empty);
        if (!DateTime.TryParseExact(rateDateText, "yyyy-MM-dd", CultureInfo.InvariantCulture, DateTimeStyles.None, out var rateDate))
            rateDate = DateTime.Today;

        rate = new ExchangeRateModel
        {
            SaleRate = saleRate,
            PurchaseRate = purchaseRate,
            RateDate = rateDate
        };

        _cachedRate = rate;
        _cachedLocalDate = todayKey;
        return true;
    }

    private void SaveTodayCachedRate(ExchangeRateModel rate)
    {
        var todayKey = GetLocalDateKey(DateTime.Today);

        _cachedRate = rate;
        _cachedLocalDate = todayKey;

        Preferences.Default.Set(CacheDateKey, todayKey);
        Preferences.Default.Set(SaleRateKey, rate.SaleRate.ToString(CultureInfo.InvariantCulture));
        Preferences.Default.Set(PurchaseRateKey, rate.PurchaseRate.ToString(CultureInfo.InvariantCulture));
        Preferences.Default.Set(RateDateKey, rate.RateDate.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture));
    }

    private static string GetLocalDateKey(DateTime date)
        => date.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture);

    private sealed class HaciendaDollarExchangeRateResponse
    {
        [JsonPropertyName("venta")]
        public HaciendaExchangeRateValue? Venta { get; set; }

        [JsonPropertyName("compra")]
        public HaciendaExchangeRateValue? Compra { get; set; }
    }

    private sealed class HaciendaExchangeRateValue
    {
        [JsonPropertyName("fecha")]
        public DateTime? Fecha { get; set; }

        [JsonPropertyName("valor")]
        public decimal Valor { get; set; }
    }
}
