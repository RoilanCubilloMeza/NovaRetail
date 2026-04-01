using System.Net.Http;
using System.Net.Http.Json;
using System.Text;
using Microsoft.Extensions.Logging;
using Newtonsoft.Json;
using NovaRetail.Models;
using NovaRetail.Models.Dtos;
using NovaRetail.Services;

namespace NovaRetail.Data
{

    public sealed class ApiClienteService : IClienteService
    {
        private const string ClientName = "NovaCustomers";

        private readonly Utilities _utilities;
        private readonly IHttpClientFactory _httpClientFactory;
        private readonly ILogger<ApiClienteService> _logger;
        private readonly string[] _baseUrls;
        private List<ActividadDto> _cachedActividades = new();
        private string _cachedActividadesCedula = string.Empty;

        public ApiClienteService(Utilities utilities, IHttpClientFactory httpClientFactory, ILogger<ApiClienteService> logger, ApiSettings settings)
        {
            _utilities = utilities;
            _httpClientFactory = httpClientFactory;
            _logger = logger;
            _baseUrls = settings.BaseUrls;
        }

        // ──────── Buscar cliente existente en la BD via API ────────

        public async Task<ClienteModel?> BuscarPorIdAsync(string clienteId)
        {
            if (string.IsNullOrWhiteSpace(clienteId))
                return null;

            foreach (var baseUrl in _baseUrls)
            {
                try
                {
                    var url = $"{baseUrl}/api/Customers?criteria={Uri.EscapeDataString(clienteId.Trim())}";
                    var http = _httpClientFactory.CreateClient(ClientName);
                    var json = await http.GetStringAsync(url);
                    var results = JsonConvert.DeserializeObject<List<ApiCustomer>>(json);

                    var match = results?.FirstOrDefault(c =>
                        string.Equals((c.AccountNumber ?? string.Empty).Trim(), clienteId.Trim(), StringComparison.OrdinalIgnoreCase))
                        ?? results?.FirstOrDefault();

                    if (match is null)
                        continue;

                    return MapToClienteModel(match);
                }
                catch (Exception ex)
                {
                    _logger.LogWarning(ex, "Error al buscar cliente desde {BaseUrl}", baseUrl);
                }
            }

            return null;
        }

        // ──────── Sincronizar con Hacienda / GoMeta ────────

        public async Task<IReadOnlyList<CustomerLookupModel>> BuscarClientesAsync(string? criteria)
        {
            foreach (var baseUrl in _baseUrls)
            {
                try
                {
                    var http = _httpClientFactory.CreateClient(ClientName);
                    string url;

                    if (string.IsNullOrWhiteSpace(criteria))
                        url = $"{baseUrl}/api/Customers";
                    else
                        url = $"{baseUrl}/api/Customers?criteria={Uri.EscapeDataString(criteria.Trim())}";

                    var json = await http.GetStringAsync(url);
                    var results = JsonConvert.DeserializeObject<List<ApiCustomer>>(json);

                    if (results is null)
                        continue;

                    return results.Select(c => new CustomerLookupModel
                    {
                        AccountNumber = c.AccountNumber ?? string.Empty,
                        FirstName = c.FirstName ?? string.Empty,
                        LastName = c.LastName ?? string.Empty,
                        Phone = c.PhoneNumber1 ?? c.PhoneNumber ?? string.Empty,
                        Email = c.EmailAddress ?? string.Empty,
                        Address = c.Address ?? string.Empty,
                        City = FirstNonEmpty(c.City, c.CITY) ?? string.Empty,
                        State = FirstNonEmpty(c.State, c.STATE) ?? string.Empty,
                        Zip = FirstNonEmpty(c.Zip, c.ZIP) ?? string.Empty
                    }).ToList();
                }
                catch (Exception ex)
                {
                    _logger.LogWarning(ex, "Error al buscar clientes desde {BaseUrl}", baseUrl);
                }
            }

            return Array.Empty<CustomerLookupModel>();
        }

        // ──────── Sincronizar con Hacienda / GoMeta (original) ────────

        public async Task<ClienteModel?> SincronizarHaciendaAsync(string clienteId)
        {
            var datos = await _utilities.GetDatosCedulaAsync(clienteId);
            if (datos is null)
                return null;

            var nombre = datos.FullName;
            if (string.IsNullOrWhiteSpace(nombre))
                nombre = string.Join(" ", new[] { datos.FirstName, datos.LastName }.Where(x => !string.IsNullOrWhiteSpace(x)));

            var actividades = (datos.Actividades ?? [])
                .Select(a => new
                {
                    Codigo = ActivityCodeHelper.Normalize(a.Codigo ?? a.CIIU4),
                    Descripcion = string.IsNullOrWhiteSpace(a.Descripcion) ? a.CIIU4desc : a.Descripcion
                })
                .Where(a => !string.IsNullOrWhiteSpace(a.Codigo))
                .Take(5)
                .ToList();

            _cachedActividades = datos.Actividades ?? [];
            _cachedActividadesCedula = clienteId;

            return new ClienteModel
            {
                ClientId = clienteId,
                IdType = ResolveIdType(datos.TipoIdentificacion, clienteId),
                Name = string.IsNullOrWhiteSpace(nombre) ? string.Empty : nombre,
                ActivityCodes = actividades.Select(a => a.Codigo!).ToList(),
                ActivityCode = string.Join(", ", actividades.Select(a => a.Codigo)),
                ActivityDescription = string.Join("; ", actividades.Select(a => a.Descripcion).Where(x => !string.IsNullOrWhiteSpace(x)))
            };
        }

        // ──────── Guardar cliente via POST a la API ────────

        public async Task<bool> GuardarAsync(ClienteModel cliente)
        {
            var apiCustomer = MapToApiCustomer(cliente);

            foreach (var baseUrl in _baseUrls)
            {
                try
                {
                    var url = $"{baseUrl}/api/Customers";
                    var http = _httpClientFactory.CreateClient(ClientName);
                    var payload = JsonConvert.SerializeObject(new List<ApiCustomer> { apiCustomer },
                        new JsonSerializerSettings { NullValueHandling = NullValueHandling.Ignore });
                    using var content = new StringContent(payload, Encoding.UTF8, "application/json");
                    var response = await http.PostAsync(url, content);
                    return response.IsSuccessStatusCode;
                }
                catch (Exception ex)
                {
                    _logger.LogWarning(ex, "Error al guardar cliente en {BaseUrl}", baseUrl);
                }
            }

            return false;
        }

        // ──────── Buscar descripción de actividad económica ────────

        public async Task<string> BuscarActividadAsync(string codActividad)
        {
            if (string.IsNullOrWhiteSpace(codActividad))
                return string.Empty;

            var codes = codActividad
                .Split([',', ';'], StringSplitOptions.RemoveEmptyEntries)
                .Select(c => c.Trim())
                .Where(c => !string.IsNullOrWhiteSpace(c))
                .ToList();

            if (codes.Count == 0)
                return string.Empty;

            // Buscar en actividades cacheadas del último sync
            if (_cachedActividades.Count > 0)
            {
                var descriptions = new List<string>();
                foreach (var code in codes)
                {
                    var normalized = ActivityCodeHelper.Normalize(code);
                    var match = _cachedActividades.FirstOrDefault(a =>
                        ActivityCodeHelper.Normalize(a.Codigo ?? a.CIIU4) == normalized);

                    if (match is not null)
                    {
                        var desc = string.IsNullOrWhiteSpace(match.Descripcion) ? match.CIIU4desc : match.Descripcion;
                        if (!string.IsNullOrWhiteSpace(desc))
                            descriptions.Add($"{normalized}: {desc.Trim()}");
                    }
                }

                if (descriptions.Count > 0)
                    return string.Join("; ", descriptions);
            }

            // Si no hay cache, intentar lookup con cédula cacheada via Hacienda
            if (!string.IsNullOrWhiteSpace(_cachedActividadesCedula))
            {
                var datos = await _utilities.GetDatosCedulaAsync(_cachedActividadesCedula);
                if (datos is not null && datos.Actividades.Count > 0)
                {
                    _cachedActividades = datos.Actividades;
                    var descriptions = new List<string>();
                    foreach (var code in codes)
                    {
                        var normalized = ActivityCodeHelper.Normalize(code);
                        var match = datos.Actividades.FirstOrDefault(a =>
                            ActivityCodeHelper.Normalize(a.Codigo ?? a.CIIU4) == normalized);

                        if (match is not null)
                        {
                            var desc = string.IsNullOrWhiteSpace(match.Descripcion) ? match.CIIU4desc : match.Descripcion;
                            if (!string.IsNullOrWhiteSpace(desc))
                                descriptions.Add($"{normalized}: {desc.Trim()}");
                        }
                    }

                    if (descriptions.Count > 0)
                        return string.Join("; ", descriptions);
                }
            }

            return "Código no encontrado. Sincronice primero con la cédula del cliente.";
        }

        // ──────── Tipos de cliente (estándar RMH) ────────

        public Task<IReadOnlyList<string>> ObtenerTiposClienteAsync()
        {
            IReadOnlyList<string> tipos = new[]
            {
                "Contado",
                "Crédito",
                "Gobierno",
                "Exportación"
            };
            return Task.FromResult(tipos);
        }

        // ──────── Tipos de identificación (desde BD) ────────

        public async Task<IReadOnlyList<string>> ObtenerTiposIdentificacionAsync()
        {
            foreach (var baseUrl in _baseUrls)
            {
                try
                {
                    var url = $"{baseUrl}/api/Utilidades/GetTipoIdentificacion";
                    var http = _httpClientFactory.CreateClient(ClientName);
                    var json = await http.GetStringAsync(url);
                    var items = JsonConvert.DeserializeObject<List<CommondEntitieDto>>(json);
                    if (items is { Count: > 0 })
                        return items.Select(i => i.StrDescripcion ?? string.Empty)
                                    .Where(d => !string.IsNullOrWhiteSpace(d))
                                    .ToList();
                }
                catch (Exception ex)
                {
                    _logger.LogWarning(ex, "Error al obtener tipos de identificación desde {BaseUrl}", baseUrl);
                }
            }

            return new[]
            {
                "Cédula Física",
                "Cédula Jurídica",
                "DIMEX",
                "NITE",
                "Extranjero No Domiciliado",
                "No Contribuyente"
            };
        }

        // ──────── Mapeo API → ClienteModel ────────

        private static ClienteModel MapToClienteModel(ApiCustomer c)
        {
            var name = string.Join(" ",
                new[] { c.FirstName, c.LastName }
                .Where(x => !string.IsNullOrWhiteSpace(x)));

            return new ClienteModel
            {
                ClientId = c.AccountNumber ?? string.Empty,
                IdType = ResolveIdType(null, c.AccountNumber),
                Name = name,
                Phone = c.PhoneNumber1 ?? c.PhoneNumber ?? string.Empty,
                Email = c.EmailAddress ?? string.Empty,
                Email2 = c.Email2 ?? string.Empty,
                Province = FirstNonEmpty(c.State, c.STATE),
                Canton = FirstNonEmpty(c.City, c.CITY),
                District = FirstNonEmpty(c.City2, c.CITY2),
                Barrio = FirstNonEmpty(c.Zip, c.ZIP),
                Address = c.Address ?? string.Empty,
                ActivityCode = c.ActivityCode ?? string.Empty,
                ActivityCodes = ParseActivityCodes(c.ActivityCode ?? string.Empty),
                CustomerType = MapAccountTypeIdToName(c.AccountTypeID > 0 ? c.AccountTypeID : c.PriceLevel),
                IsReceiver = !string.IsNullOrWhiteSpace(c.EmailAddress)
            };
        }

        private static string? FirstNonEmpty(params string?[] values)
        {
            return values.FirstOrDefault(v => !string.IsNullOrWhiteSpace(v))?.Trim();
        }

        // ──────── Mapeo ClienteModel → API ────────

        private static ApiCustomer MapToApiCustomer(ClienteModel m)
        {
            var parts = (m.Name ?? string.Empty).Split(' ', 2, StringSplitOptions.RemoveEmptyEntries);
            var firstName = parts.Length > 0 ? parts[0] : "N/A";
            var lastName = parts.Length > 1 ? parts[1] : "N/A";

            return new ApiCustomer
            {
                AccountNumber = m.ClientId,
                FirstName = firstName,
                LastName = lastName,
                PhoneNumber1 = m.Phone,
                PhoneNumber2 = string.Empty,
                EmailAddress = m.Email,
                Email2 = m.Email2 ?? string.Empty,
                State = m.Province ?? string.Empty,
                City = m.Canton ?? string.Empty,
                City2 = m.District ?? string.Empty,
                Zip = m.Barrio ?? string.Empty,
                Address = m.Address,
                ActivityCode = m.ActivityCode,
                AccountTypeID = MapCustomerTypeToId(m.CustomerType),
                Source = "WC_API",
                Vendedor = "WC_API",
                CreditDays = 0
            };
        }

        private static List<string> ParseActivityCodes(string value)
        {
            if (string.IsNullOrWhiteSpace(value))
                return [];

            return value
                .Split(new[] { ',', ';' }, StringSplitOptions.RemoveEmptyEntries)
                .Select(x => x.Trim())
                .Where(x => !string.IsNullOrWhiteSpace(x))
                .Distinct(StringComparer.OrdinalIgnoreCase)
                .ToList();
        }

        // ──────── AccountTypeID ↔ Nombre ────────

        private static string MapAccountTypeIdToName(int id) => id switch
        {
            1 => "Contado",
            2 => "Crédito",
            3 => "Gobierno",
            4 => "Exportación",
            _ => "Contado"
        };

        private static int MapCustomerTypeToId(string type) => type switch
        {
            "Crédito" => 2,
            "Gobierno" => 3,
            "Exportación" => 4,
            _ => 1
        };

        // ──────── Normalizar código de actividad ────────

        private static string ResolveIdType(string? identificationCode, string? clientId)
        {
            var mapped = identificationCode?.Trim() switch
            {
                "01" => "Cédula Física",
                "02" => "Cédula Jurídica",
                "03" => "DIMEX",
                "04" => "NITE",
                "05" => "Extranjero No Domiciliado",
                "06" => "No Contribuyente",
                _ => string.Empty
            };

            if (!string.IsNullOrWhiteSpace(mapped))
                return mapped;

            var digits = new string((clientId ?? string.Empty).Where(char.IsDigit).ToArray());
            return digits.Length switch
            {
                9 => "Cédula Física",
                10 => "Cédula Jurídica",
                12 => "DIMEX",
                _ => "Cédula Física"
            };
        }

        // ──────── DTO para comunicación con la API ────────

        public class ApiCustomer
        {
            [JsonProperty("ID")]
            public int ID { get; set; }

            [JsonProperty("AccountTypeID")]
            public int AccountTypeID { get; set; }

            [JsonProperty("PriceLevel")]
            public int PriceLevel { get; set; }

            [JsonProperty("AccountNumber")]
            public string? AccountNumber { get; set; }

            [JsonProperty("FirstName")]
            public string? FirstName { get; set; }

            [JsonProperty("LastName")]
            public string? LastName { get; set; }

            [JsonProperty("PhoneNumber1")]
            public string? PhoneNumber1 { get; set; }

            [JsonProperty("PhoneNumber")]
            public string? PhoneNumber { get; set; }

            [JsonProperty("PhoneNumber2")]
            public string? PhoneNumber2 { get; set; }

            [JsonProperty("EmailAddress")]
            public string? EmailAddress { get; set; }

            [JsonProperty("Email2")]
            public string? Email2 { get; set; }

            [JsonProperty("State")]
            public string? State { get; set; }

            [JsonProperty("STATE")]
            public string? STATE { get; set; }

            [JsonProperty("City")]
            public string? City { get; set; }

            [JsonProperty("CITY")]
            public string? CITY { get; set; }

            [JsonProperty("City2")]
            public string? City2 { get; set; }

            [JsonProperty("CITY2")]
            public string? CITY2 { get; set; }

            [JsonProperty("Zip")]
            public string? Zip { get; set; }

            [JsonProperty("ZIP")]
            public string? ZIP { get; set; }

            [JsonProperty("Address")]
            public string? Address { get; set; }

            [JsonProperty("ActivityCode")]
            public string? ActivityCode { get; set; }

            [JsonProperty("CreditDays")]
            public int? CreditDays { get; set; }

            [JsonProperty("Source")]
            public string? Source { get; set; }

            [JsonProperty("Vendedor")]
            public string? Vendedor { get; set; }
        }
    }
}
