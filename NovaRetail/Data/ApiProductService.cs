using System.Net.Http;
using System.Net.Http.Json;
using Newtonsoft.Json;
using NovaRetail.Models;

namespace NovaRetail.Data
{
    public class ApiProductService : IProductService
    {
        private const string ItemsClientName = "NovaItems";
        private const string ReasonCodeClientName = "NovaReasonCodes";

        private static readonly string[] ItemsBaseUrls =
        {
            "http://localhost:52500",
            "http://127.0.0.1:52500"
        };

        private readonly IHttpClientFactory _httpClientFactory;

        public ApiProductService(IHttpClientFactory httpClientFactory)
        {
            _httpClientFactory = httpClientFactory;
        }

        public async Task<List<ProductModel>> GetProductsAsync(int page, int pageSize, decimal exchangeRate)
        {
            var safePage = page < 1 ? 1 : page;
            var safePageSize = Math.Clamp(pageSize, 1, 500);

            foreach (var baseUrl in ItemsBaseUrls)
            {
                try
                {
                    var http = _httpClientFactory.CreateClient(ItemsClientName);
                    var url = $"{baseUrl}/api/Items?storeid=1&tipo=1&page={safePage}&pageSize={safePageSize}";
                    var apiItems = await http.GetFromJsonAsync<List<ApiItem>>(url);

                    if (apiItems is null || apiItems.Count == 0)
                        continue;

                    return apiItems.Select(item => MapToProduct(item, exchangeRate)).ToList();
                }
                catch
                {
                }
            }

            return [];
        }

        public async Task<List<ProductModel>> SearchAsync(string criteria, int top, decimal exchangeRate)
        {
            var safeTop = Math.Clamp(top, 1, 1000);

            foreach (var baseUrl in ItemsBaseUrls)
            {
                try
                {
                    var http = _httpClientFactory.CreateClient(ItemsClientName);
                    var url = $"{baseUrl}/api/Items/Search?criteria={Uri.EscapeDataString(criteria)}&top={safeTop}";
                    var apiItems = await http.GetFromJsonAsync<List<ApiItem>>(url);

                    if (apiItems is null || apiItems.Count == 0)
                        continue;

                    return apiItems.Select(item => MapToProduct(item, exchangeRate)).ToList();
                }
                catch
                {
                }
            }

            return [];
        }

        public async Task<List<ReasonCodeModel>> GetReasonCodesAsync(int type)
        {
            foreach (var baseUrl in ItemsBaseUrls)
            {
                try
                {
                    var http = _httpClientFactory.CreateClient(ReasonCodeClientName);
                    var url = $"{baseUrl}/api/ReasonCodes?type={type}";
                    var json = await http.GetStringAsync(url);
                    var codes = JsonConvert.DeserializeObject<List<ReasonCodeModel>>(json);

                    if (codes is not null && codes.Count > 0)
                        return codes;
                }
                catch
                {
                }
            }

            return [];
        }

        public async Task<int> GetProductCountAsync()
        {
            foreach (var baseUrl in ItemsBaseUrls)
            {
                try
                {
                    var http = _httpClientFactory.CreateClient(ItemsClientName);
                    var url = $"{baseUrl}/api/Items/Count?storeid=1&tipo=1";
                    var result = await http.GetFromJsonAsync<ProductCountResult>(url);
                    if (result is not null && result.Total > 0)
                        return result.Total;
                }
                catch
                {
                }
            }

            return 0;
        }

        private static ProductModel MapToProduct(ApiItem item, decimal exchangeRate)
        {
            var priceColones = item.PRICE > 0 ? item.PRICE : item.PriceA;
            var priceDollars = exchangeRate > 0 ? Math.Round(priceColones / exchangeRate, 2) : priceColones;

            return new ProductModel
            {
                Name = string.IsNullOrWhiteSpace(item.Description)
                    ? item.ExtendedDescription ?? string.Empty
                    : item.Description,
                Code = item.ItemLookupCode ?? item.ID.ToString(),
                PriceValue = priceDollars,
                Price = $"${priceDollars:F2}",
                Category = DetermineCategory(item),
                Stock = Convert.ToDecimal(item.Quantity ?? 0),
                PriceColonesValue = priceColones,
                TaxPercentage = NormalizeTaxPercentage(item.Percentage),
                TaxId = item.TaxID,
                Cabys = NormalizeCabys(item.SubDescription3)
            };
        }

        private static decimal NormalizeTaxPercentage(float percentage)
            => percentage <= 0 ? 0m : Convert.ToDecimal(percentage);

        private static string NormalizeCabys(string? cabys)
        {
            if (string.IsNullOrWhiteSpace(cabys))
                return string.Empty;

            var normalized = new string(cabys.Where(char.IsDigit).ToArray());
            return normalized.Length == 13 ? normalized : string.Empty;
        }

        private static string DetermineCategory(ApiItem item)
        {
            var text = $"{item.Description} {item.ExtendedDescription} {item.SubDescription2}".ToLowerInvariant();

            if (text.Contains("sandalia") || text.Contains("zapato") || text.Contains("tenis") ||
                text.Contains("zapat") || text.Contains("bota") || text.Contains("calcetin") ||
                text.Contains("plantilla"))
                return "Calzado";

            if (text.Contains("martillo") || text.Contains("tornillo") || text.Contains("clavo") ||
                text.Contains("llave") || text.Contains("pintura") || text.Contains("broca") ||
                text.Contains("cinta") || text.Contains("pvc") || text.Contains("taco") ||
                text.Contains("ferreter"))
                return "Ferreteria";

            if (text.Contains("escoba") || text.Contains("cojin") || text.Contains("cubeta") ||
                text.Contains("almohada") || text.Contains("hogar") || text.Contains("vela") ||
                text.Contains("limpiador"))
                return "Hogar";

            return "Supermercado";
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
            public string? Description { get; set; }
            public string? SubDescription2 { get; set; }
            public string? SubDescription3 { get; set; }
            public int TaxID { get; set; }
            public float Percentage { get; set; }
        }

        private sealed class ProductCountResult
        {
            public int Total { get; set; }
        }
    }
}
