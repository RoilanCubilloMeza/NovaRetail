using Microsoft.Extensions.Logging;
using Newtonsoft.Json;
using NovaRetail.Models;

namespace NovaRetail.Data;

public sealed class ApiExonerationService : IExonerationService
{
    private const string ClientName = "NovaExoneration";
    private const string BaseUrl = "https://api.hacienda.go.cr";

    private readonly IHttpClientFactory _httpClientFactory;
    private readonly ILogger<ApiExonerationService> _logger;

    public ApiExonerationService(IHttpClientFactory httpClientFactory, ILogger<ApiExonerationService> logger)
    {
        _httpClientFactory = httpClientFactory;
        _logger = logger;
    }

    public async Task<ExonerationValidationResult> ValidateAsync(string authorization, CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(authorization))
        {
            return new ExonerationValidationResult
            {
                IsValid = false,
                Message = "Ingrese un número de autorización válido."
            };
        }

        try
        {
            var http = _httpClientFactory.CreateClient(ClientName);
            var url = $"{BaseUrl}/fe/ex?autorizacion={Uri.EscapeDataString(authorization.Trim())}";
            using var response = await http.GetAsync(url, cancellationToken);

            if (response.StatusCode == System.Net.HttpStatusCode.BadRequest)
            {
                return new ExonerationValidationResult
                {
                    IsValid = false,
                    Message = "El código de autorización no fue reconocido por Hacienda. Verifique el número ingresado (Ej. AL-00020402-24)."
                };
            }

            if (!response.IsSuccessStatusCode)
            {
                return new ExonerationValidationResult
                {
                    IsValid = false,
                    Message = "No fue posible validar la exoneración en Hacienda."
                };
            }

            var json = await response.Content.ReadAsStringAsync(cancellationToken);
            var apiResponse = JsonConvert.DeserializeObject<ExonerationApiResponse>(json);
            if (apiResponse is null)
            {
                return new ExonerationValidationResult
                {
                    IsValid = false,
                    Message = "Hacienda no devolvió datos de exoneración válidos."
                };
            }

            return new ExonerationValidationResult
            {
                IsValid = true,
                Document = new ExonerationModel
                {
                    NumeroDocumento = apiResponse.NumeroDocumento ?? string.Empty,
                    Identificacion = apiResponse.Identificacion ?? string.Empty,
                    PorcentajeExoneracion = apiResponse.PorcentajeExoneracion,
                    Autorizacion = apiResponse.Autorizacion,
                    FechaEmision = apiResponse.FechaEmision,
                    FechaVencimiento = apiResponse.FechaVencimiento,
                    Ano = apiResponse.Ano,
                    Cabys = apiResponse.Cabys ?? [],
                    TipoAutorizacion = apiResponse.TipoAutorizacion ?? string.Empty,
                    TipoDocumentoCodigo = apiResponse.TipoDocumento?.Codigo ?? string.Empty,
                    TipoDocumentoDescripcion = apiResponse.TipoDocumento?.Descripcion ?? string.Empty,
                    CodigoInstitucion = apiResponse.CodigoInstitucion ?? string.Empty,
                    NombreInstitucion = apiResponse.NombreInstitucion ?? string.Empty,
                    PoseeCabys = apiResponse.PoseeCabys
                }
            };
        }
        catch (OperationCanceledException)
        {
            throw;
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Error al validar exoneración con Hacienda");
            return new ExonerationValidationResult
            {
                IsValid = false,
                Message = "No fue posible consultar la exoneración en este momento."
            };
        }
    }

    private sealed class ExonerationApiResponse
    {
        [JsonProperty("numeroDocumento")]
        public string? NumeroDocumento { get; set; }

        [JsonProperty("identificacion")]
        public string? Identificacion { get; set; }

        [JsonProperty("porcentajeExoneracion")]
        public decimal PorcentajeExoneracion { get; set; }

        [JsonProperty("autorizacion")]
        public int Autorizacion { get; set; }

        [JsonProperty("fechaEmision")]
        public DateTime? FechaEmision { get; set; }

        [JsonProperty("fechaVencimiento")]
        public DateTime? FechaVencimiento { get; set; }

        [JsonProperty("ano")]
        public int Ano { get; set; }

        [JsonProperty("cabys")]
        public List<string>? Cabys { get; set; }

        [JsonProperty("tipoAutorizacion")]
        public string? TipoAutorizacion { get; set; }

        [JsonProperty("tipoDocumento")]
        public ExonerationDocumentTypeApiResponse? TipoDocumento { get; set; }

        [JsonProperty("CodigoInstitucion")]
        public string? CodigoInstitucion { get; set; }

        [JsonProperty("nombreInstitucion")]
        public string? NombreInstitucion { get; set; }

        [JsonProperty("poseeCabys")]
        public bool PoseeCabys { get; set; }
    }

    private sealed class ExonerationDocumentTypeApiResponse
    {
        [JsonProperty("codigo")]
        public string? Codigo { get; set; }

        [JsonProperty("descripcion")]
        public string? Descripcion { get; set; }
    }
}
