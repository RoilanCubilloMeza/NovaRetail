using System.Net.Http.Json;
using Microsoft.Extensions.Logging;
using Newtonsoft.Json;
using NovaRetail.Models;

namespace NovaRetail.Data;

public sealed class ApiProductService : IProductService
{
    private const string ItemsClientName = "NovaItems";
    private const string ReasonCodeClientName = "NovaReasonCodes";

    private readonly IHttpClientFactory _httpClientFactory;
    private readonly ILogger<ApiProductService> _logger;
    private readonly string[] _baseUrls;

    private int _cachedProductCount;
    private DateTime _countCacheExpiry;
    private readonly Dictionary<int, (List<ReasonCodeModel> Codes, DateTime Expiry)> _reasonCodesCache = new();
    private static readonly TimeSpan ReasonCodesCacheDuration = TimeSpan.FromMinutes(10);

    public ApiProductService(IHttpClientFactory httpClientFactory, ILogger<ApiProductService> logger, ApiSettings settings)
    {
        _httpClientFactory = httpClientFactory;
        _logger = logger;
        _baseUrls = settings.BaseUrls;
    }

    public async Task<List<ProductModel>> GetProductsAsync(int page, int pageSize, decimal exchangeRate, int storeId = 1, CancellationToken cancellationToken = default)
    {
        var safePage = page < 1 ? 1 : page;
        var safePageSize = Math.Clamp(pageSize, 1, 500);
        var safeStoreId = storeId > 0 ? storeId : 1;
        var deptMap = await GetDepartmentMapAsync();

        foreach (var baseUrl in _baseUrls)
        {
            try
            {
                var http = _httpClientFactory.CreateClient(ItemsClientName);
                var url = $"{baseUrl}/api/Items?storeid={safeStoreId}&tipo=1&page={safePage}&pageSize={safePageSize}";
                var apiItems = await http.GetFromJsonAsync<List<ApiItem>>(url, cancellationToken);

                if (apiItems is null || apiItems.Count == 0)
                    continue;

                return apiItems.Select(item => MapToProduct(item, exchangeRate, deptMap)).ToList();
            }
            catch (Exception ex)
            {
                if (ex is OperationCanceledException)
                    throw;

                _logger.LogWarning(ex, "Error al obtener productos desde {BaseUrl}", baseUrl);
            }
        }

        return [];
    }

    public async Task<ProductModel?> GetByIdAsync(int itemId, decimal exchangeRate)
    {
        if (itemId <= 0)
            return null;

        var deptMap = await GetDepartmentMapAsync();

        foreach (var baseUrl in _baseUrls)
        {
            try
            {
                var http = _httpClientFactory.CreateClient(ItemsClientName);
                var url = $"{baseUrl}/api/Items/{itemId}";
                var apiItem = await http.GetFromJsonAsync<ApiItem>(url);

                if (apiItem is null || apiItem.ID <= 0)
                    continue;

                return MapToProduct(apiItem, exchangeRate, deptMap);
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "Error al obtener producto {ItemId} desde {BaseUrl}", itemId, baseUrl);
            }
        }

        return null;
    }

    public async Task<List<ProductModel>> SearchAsync(string criteria, int top, decimal exchangeRate, CancellationToken cancellationToken = default)
    {
        var safeTop = Math.Clamp(top, 1, 1000);

        foreach (var baseUrl in _baseUrls)
        {
            try
            {
                var http = _httpClientFactory.CreateClient(ItemsClientName);
                var directUrl = $"{baseUrl}/api/Items/SearchDirect?criteria={Uri.EscapeDataString(criteria)}&top={safeTop}";
                var apiItems = await TryGetApiItemsAsync(http, directUrl, cancellationToken);

                if (apiItems.Count == 0)
                {
                    var legacyUrl = $"{baseUrl}/api/Items?criteria={Uri.EscapeDataString(criteria)}";
                    apiItems = await TryGetApiItemsAsync(http, legacyUrl, cancellationToken);
                }

                if (apiItems is null || apiItems.Count == 0)
                    continue;

                return apiItems.Select(item => MapToProduct(item, exchangeRate)).ToList();
            }
            catch (Exception ex)
            {
                if (ex is OperationCanceledException)
                    throw;

                _logger.LogWarning(ex, "Error al buscar productos desde {BaseUrl}", baseUrl);
            }
        }

        return [];
    }

    private async Task<List<ApiItem>> TryGetApiItemsAsync(HttpClient http, string url, CancellationToken cancellationToken = default)
    {
        try
        {
            return await http.GetFromJsonAsync<List<ApiItem>>(url, cancellationToken) ?? [];
        }
        catch (OperationCanceledException)
        {
            throw;
        }
        catch (Exception ex)
        {
            _logger.LogDebug(ex, "No se pudieron obtener productos desde {Url}", url);
            return [];
        }
    }

    public async Task<List<ProductModel>> SearchByDepartmentAsync(int departmentId, int top, decimal exchangeRate)
    {
        var safeTop = Math.Clamp(top, 1, 1000);
        var deptMap = await GetDepartmentMapAsync();

        foreach (var baseUrl in _baseUrls)
        {
            try
            {
                var http = _httpClientFactory.CreateClient(ItemsClientName);
                var url = $"{baseUrl}/api/Items/ByDepartment?departmentId={departmentId}&top={safeTop}";
                var apiItems = await http.GetFromJsonAsync<List<ApiItem>>(url);

                if (apiItems is null || apiItems.Count == 0)
                    continue;

                return apiItems.Select(item => MapToProduct(item, exchangeRate, deptMap)).ToList();
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "Error al buscar productos por departamento desde {BaseUrl}", baseUrl);
            }
        }

        return [];
    }

    public async Task<List<ReasonCodeModel>> GetReasonCodesAsync(int type)
    {
        if (_reasonCodesCache.TryGetValue(type, out var cached) && DateTime.UtcNow < cached.Expiry)
            return cached.Codes.Select(CloneReasonCode).ToList();

        foreach (var baseUrl in _baseUrls)
        {
            try
            {
                var http = _httpClientFactory.CreateClient(ReasonCodeClientName);
                var url = $"{baseUrl}/api/ReasonCodes?type={type}";
                var json = await http.GetStringAsync(url);
                var codes = JsonConvert.DeserializeObject<List<ReasonCodeModel>>(json);

                if (codes is not null && codes.Count > 0)
                {
                    _reasonCodesCache[type] = (codes.Select(CloneReasonCode).ToList(), DateTime.UtcNow.Add(ReasonCodesCacheDuration));
                    return codes.Select(CloneReasonCode).ToList();
                }
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "Error al obtener reason codes desde {BaseUrl}", baseUrl);
            }
        }

        return [];
    }

    private static ReasonCodeModel CloneReasonCode(ReasonCodeModel code)
        => new()
        {
            ID = code.ID,
            Type = code.Type,
            Code = code.Code,
            Description = code.Description
        };

    public async Task<List<ReasonCodeModel>> GetExonerationDocumentTypesAsync()
    {
        foreach (var baseUrl in _baseUrls)
        {
            try
            {
                var http = _httpClientFactory.CreateClient(ReasonCodeClientName);
                var url = $"{baseUrl}/api/ReasonCodes/exoneration-document-types";
                var json = await http.GetStringAsync(url);
                var codes = JsonConvert.DeserializeObject<List<ReasonCodeModel>>(json);

                if (codes is not null && codes.Count > 0)
                    return codes;
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "Error al obtener tipos de documento de exoneracion desde {BaseUrl}", baseUrl);
            }
        }

        _logger.LogWarning("No se encontraron tipos de documento de exoneracion en ningun endpoint configurado.");
        return [];
    }

    public async Task<int> GetProductCountAsync(int storeId = 1)
    {
        if (_cachedProductCount > 0 && DateTime.UtcNow < _countCacheExpiry)
            return _cachedProductCount;

        var safeStoreId = storeId > 0 ? storeId : 1;
        foreach (var baseUrl in _baseUrls)
        {
            try
            {
                var http = _httpClientFactory.CreateClient(ItemsClientName);
                var url = $"{baseUrl}/api/Items/Count?storeid={safeStoreId}&tipo=1";
                var result = await http.GetFromJsonAsync<ProductCountResult>(url);
                if (result is not null && result.Total > 0)
                {
                    _cachedProductCount = result.Total;
                    _countCacheExpiry = DateTime.UtcNow.AddMinutes(5);
                    return result.Total;
                }
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "Error al obtener conteo de productos desde {BaseUrl}", baseUrl);
            }
        }

        return _cachedProductCount > 0 ? _cachedProductCount : 0;
    }

    private static ProductModel MapToProduct(ApiItem item, decimal exchangeRate, IReadOnlyDictionary<int, string>? departmentMap = null)
    {
        var priceColones = item.PRICE > 0 ? item.PRICE : item.PriceA;
        var priceDollars = exchangeRate > 0 ? Math.Round(priceColones / exchangeRate, 2) : priceColones;

        return new ProductModel
        {
            ItemID = item.ID,
            DepartmentID = item.DepartmentID,
            Name = string.IsNullOrWhiteSpace(item.Description)
                ? item.ExtendedDescription ?? string.Empty
                : item.Description,
            Code = item.ItemLookupCode ?? item.ID.ToString(),
            ExtendedDescription = item.ExtendedDescription ?? string.Empty,
            SubDescription1 = item.SubDescription1 ?? string.Empty,
            SubDescription2 = item.SubDescription2 ?? string.Empty,
            SubDescription3 = item.SubDescription3 ?? string.Empty,
            PriceValue = priceDollars,
            Price = $"${priceDollars:F2}",
            Category = ResolveCategoryFromDepartment(item.DepartmentID, departmentMap),
            Stock = Convert.ToDecimal(item.Quantity ?? 0),
            PriceColonesValue = priceColones,
            Cost = item.Cost,
            TaxPercentage = NormalizeTaxPercentage(item.Percentage),
            TaxId = item.TaxID,
            Cabys = NormalizeCabys(item.SubDescription3),
            ItemType = item.ItemType
        };
    }

    /// <summary>
    /// Resuelve el nombre de categoría usando el mapa de departamentos cargado desde la DB.
    /// Si no se tiene mapa o el DepartmentID no existe, retorna el ID como texto.
    /// </summary>
    private static string ResolveCategoryFromDepartment(int departmentId, IReadOnlyDictionary<int, string>? departmentMap)
    {
        if (departmentMap is not null && departmentMap.TryGetValue(departmentId, out var name))
            return name;

        return departmentId.ToString();
    }

    /// <summary>
    /// Carga o devuelve el mapa de departamentos (ID → Nombre) desde la API.
    /// Se almacena en caché para evitar llamadas repetidas.
    /// </summary>
    private async Task<IReadOnlyDictionary<int, string>> GetDepartmentMapAsync()
    {
        if (_departmentMap is not null)
            return _departmentMap;

        foreach (var baseUrl in _baseUrls)
        {
            try
            {
                var http = _httpClientFactory.CreateClient(ItemsClientName);
                var categories = await http.GetFromJsonAsync<List<DepartmentEntry>>($"{baseUrl}/api/Categories/All");
                if (categories is not null && categories.Count > 0)
                {
                    _departmentMap = categories.ToDictionary(c => c.ID, c => c.Name);
                    return _departmentMap;
                }
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "Error al obtener departamentos desde {BaseUrl}", baseUrl);
            }
        }

        _departmentMap = new Dictionary<int, string>();
        return _departmentMap;
    }

    private IReadOnlyDictionary<int, string>? _departmentMap;

    private static decimal NormalizeTaxPercentage(float percentage)
        => percentage <= 0 ? 0m : Convert.ToDecimal(percentage);

    private static string NormalizeCabys(string? cabys)
    {
        if (string.IsNullOrWhiteSpace(cabys))
            return string.Empty;

        var normalized = new string(cabys.Where(char.IsDigit).ToArray());
        return normalized.Length == 13 ? normalized : string.Empty;
    }

    private sealed class ApiItem
    {
        public int ID { get; set; }
        public string? ItemLookupCode { get; set; }
        public string? ExtendedDescription { get; set; }
        public double? Quantity { get; set; }
        public int DepartmentID { get; set; }
        public decimal PRICE { get; set; }
        public decimal PriceA { get; set; }
        public decimal Cost { get; set; }
        public string? Description { get; set; }
        public string? SubDescription1 { get; set; }
        public string? SubDescription2 { get; set; }
        public string? SubDescription3 { get; set; }
        public int TaxID { get; set; }
        public float Percentage { get; set; }
        public int ItemType { get; set; }
    }

    private sealed class DepartmentEntry
    {
        public int ID { get; set; }
        public string Name { get; set; } = string.Empty;
    }

    private sealed class ProductCountResult
    {
        public int Total { get; set; }
    }
}
