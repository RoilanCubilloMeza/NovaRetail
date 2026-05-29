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
    private const string EuroExchangeRateUrl = "https://api.hacienda.go.cr/indicadores/tc/euro";
    private const string CacheDateKey = "HaciendaExchangeRate.CacheDate";
    private const string SaleRateKey = "HaciendaExchangeRate.SaleRate";
    private const string PurchaseRateKey = "HaciendaExchangeRate.PurchaseRate";
    private const string RateDateKey = "HaciendaExchangeRate.RateDate";
    private const string EuroCacheDateKey = "HaciendaExchangeRate.Euro.CacheDate";
    private const string EuroSaleRateKey = "HaciendaExchangeRate.Euro.SaleRate";
    private const string EuroPurchaseRateKey = "HaciendaExchangeRate.Euro.PurchaseRate";
    private const string EuroRateDateKey = "HaciendaExchangeRate.Euro.RateDate";

    private readonly IHttpClientFactory _httpClientFactory;
    private readonly ILogger<HaciendaExchangeRateService> _logger;
    private ExchangeRateModel? _cachedRate;
    private string _cachedLocalDate = string.Empty;
    private ExchangeRateModel? _cachedEuroRate;
    private string _cachedEuroLocalDate = string.Empty;

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

    public async Task<ExchangeRateModel?> GetEuroExchangeRateAsync(
        bool forceRefresh = false,
        CancellationToken cancellationToken = default)
    {
        if (TryGetTodayEuroCachedRate(out var cachedRate))
            return cachedRate;

        try
        {
            var http = _httpClientFactory.CreateClient(ClientName);
            // El endpoint de Euro usa estructura plana: { "fecha": "...", "dolares": 1.16, "colones": 530.74 }
            var response = await http.GetFromJsonAsync<HaciendaEuroExchangeRateResponse>(
                EuroExchangeRateUrl,
                cancellationToken);

            if (response?.Colones is not > 0)
                return _cachedEuroRate;

            var rate = new ExchangeRateModel
            {
                SaleRate = response.Colones,
                PurchaseRate = response.Colones,
                RateDate = response.Fecha ?? DateTime.Today
            };

            SaveTodayEuroCachedRate(rate);
            return rate;
        }
        catch (OperationCanceledException)
        {
            throw;
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Error al consultar tipo de cambio Euro en Hacienda");
            return _cachedEuroRate;
        }
    }

    private bool TryGetTodayEuroCachedRate(out ExchangeRateModel? rate)
    {
        var todayKey = GetLocalDateKey(DateTime.Today);

        if (_cachedEuroRate is not null && _cachedEuroLocalDate == todayKey)
        {
            rate = _cachedEuroRate;
            return true;
        }

        rate = null;
        if (Preferences.Default.Get(EuroCacheDateKey, string.Empty) != todayKey)
            return false;

        var saleRateText = Preferences.Default.Get(EuroSaleRateKey, string.Empty);
        if (!decimal.TryParse(saleRateText, NumberStyles.Number, CultureInfo.InvariantCulture, out var saleRate)
            || saleRate <= 0)
        {
            return false;
        }

        var purchaseRateText = Preferences.Default.Get(EuroPurchaseRateKey, string.Empty);
        decimal.TryParse(purchaseRateText, NumberStyles.Number, CultureInfo.InvariantCulture, out var purchaseRate);

        var rateDateText = Preferences.Default.Get(EuroRateDateKey, string.Empty);
        if (!DateTime.TryParseExact(rateDateText, "yyyy-MM-dd", CultureInfo.InvariantCulture, DateTimeStyles.None, out var rateDate))
            rateDate = DateTime.Today;

        rate = new ExchangeRateModel
        {
            SaleRate = saleRate,
            PurchaseRate = purchaseRate,
            RateDate = rateDate
        };

        _cachedEuroRate = rate;
        _cachedEuroLocalDate = todayKey;
        return true;
    }

    private void SaveTodayEuroCachedRate(ExchangeRateModel rate)
    {
        var todayKey = GetLocalDateKey(DateTime.Today);

        _cachedEuroRate = rate;
        _cachedEuroLocalDate = todayKey;

        Preferences.Default.Set(EuroCacheDateKey, todayKey);
        Preferences.Default.Set(EuroSaleRateKey, rate.SaleRate.ToString(CultureInfo.InvariantCulture));
        Preferences.Default.Set(EuroPurchaseRateKey, rate.PurchaseRate.ToString(CultureInfo.InvariantCulture));
        Preferences.Default.Set(EuroRateDateKey, rate.RateDate.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture));
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

    // Estructura plana del endpoint /tc/euro: { "fecha": "...", "dolares": 1.16, "colones": 530.74 }
    private sealed class HaciendaEuroExchangeRateResponse
    {
        [JsonPropertyName("fecha")]
        public DateTime? Fecha { get; set; }

        [JsonPropertyName("dolares")]
        public decimal Dolares { get; set; }

        [JsonPropertyName("colones")]
        public decimal Colones { get; set; }
    }
}
