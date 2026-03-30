using Microsoft.Extensions.Logging;
using Newtonsoft.Json;
using NovaRetail.Models.Dtos;
using System.Net.Mail;
using System.Text;
using System.Text.RegularExpressions;

namespace NovaRetail.Services;

/// <summary>
/// Utilidades generales: consultas externas de cédula, formato de fechas,
/// codificación y validación de correo.
/// </summary>
public sealed class Utilities
{
    private const string ExternalClientName = "NovaExternal";

    private readonly IHttpClientFactory _httpClientFactory;
    private readonly ILogger<Utilities> _logger;

    public Utilities(IHttpClientFactory httpClientFactory, ILogger<Utilities> logger)
    {
        _httpClientFactory = httpClientFactory;
        _logger = logger;
    }

    // ── Fechas ──

    public static string GetFormattedCurrentDate()
        => DateTime.Now.ToString("yyyy-MM-dd HH':'mm':'ss");

    public static string GetFormattedDatePlus30Days()
        => DateTime.Now.AddDays(30).ToString("yyyy-MM-dd HH':'mm':'ss");

    public static string GetCurrentDateReference()
        => DateTime.Now.ToString("yyyyMMdd");

    // ── Validación ──

    public static bool ValidateEmail(string email)
    {
        if (string.IsNullOrWhiteSpace(email))
            return false;

        return MailAddress.TryCreate(email.Trim(), out _);
    }

    // ── Codificación ──

    public static string DecodeBase64(string base64EncodedData)
    {
        var bytes = Convert.FromBase64String(base64EncodedData);
        return Encoding.UTF8.GetString(bytes);
    }

    public static string EncodeBase64(string plainText)
    {
        var bytes = Encoding.UTF8.GetBytes(plainText);
        return Convert.ToBase64String(bytes);
    }

    // ── Texto ──

    public static string RemoveDiacritics(string text)
        => Regex.Replace(text.Normalize(NormalizationForm.FormD), @"[^a-zA-z0-9 ]+", "");

    // ── Consulta de cédula (fuentes externas) ──

    public async Task<CedulaDatosDto?> GetDatosCedulaAsync(string cedula)
    {
        using var cts = new CancellationTokenSource(TimeSpan.FromSeconds(15));

        try
        {
            var hacienda = await GetDatosHaciendaAsync(cedula, cts.Token);
            if (hacienda is not null && hacienda.Actividades.Count > 0)
                return hacienda;

            var goMeta = await GetDatosGoMetaAsync(cedula, cts.Token);
            if (goMeta is null)
                return hacienda;

            if (hacienda is null)
                return goMeta;

            if (string.IsNullOrWhiteSpace(goMeta.FullName))
                goMeta.FullName = hacienda.FullName;

            if (string.IsNullOrWhiteSpace(goMeta.FirstName))
                goMeta.FirstName = hacienda.FirstName;

            if (string.IsNullOrWhiteSpace(goMeta.LastName))
                goMeta.LastName = hacienda.LastName;

            if (string.IsNullOrWhiteSpace(goMeta.RegimenDescripcion))
                goMeta.RegimenDescripcion = hacienda.RegimenDescripcion;

            if (string.IsNullOrWhiteSpace(goMeta.TipoIdentificacion))
                goMeta.TipoIdentificacion = hacienda.TipoIdentificacion;

            if (string.IsNullOrWhiteSpace(goMeta.SituacionEstado))
                goMeta.SituacionEstado = hacienda.SituacionEstado;

            if (goMeta.Actividades.Count == 0)
                goMeta.Actividades = hacienda.Actividades;

            return goMeta;
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Error al obtener datos de cédula {Cedula}", cedula);
            return null;
        }
    }

    private async Task<CedulaDatosDto?> GetDatosHaciendaAsync(string cedula, CancellationToken cancellationToken)
    {
        var url = $"https://api.hacienda.go.cr/fe/ae?identificacion={cedula}";

        try
        {
            var http = _httpClientFactory.CreateClient(ExternalClientName);
            using var response = await http.GetAsync(url, cancellationToken);
            if (!response.IsSuccessStatusCode)
                return null;

            var responseBody = await response.Content.ReadAsStringAsync(cancellationToken);
            var parsed = JsonConvert.DeserializeObject<HaciendaResponse>(responseBody);
            if (parsed == null)
                return null;

            return new CedulaDatosDto
            {
                FullName = parsed.Nombre,
                RegimenDescripcion = parsed.Regimen?.Descripcion,
                TipoIdentificacion = parsed.TipoIdentificacion,
                SituacionEstado = parsed.Situacion?.Estado,
                Actividades = parsed.Actividades?.Select(a => new ActividadDto
                {
                    Codigo = a.Codigo,
                    Descripcion = a.Descripcion,
                    CIIU4 = a.Codigo,
                    CIIU4desc = a.Descripcion
                }).ToList() ?? []
            };
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Error al consultar Hacienda para cédula {Cedula}", cedula);
            return null;
        }
    }

    private async Task<CedulaDatosDto?> GetDatosGoMetaAsync(string cedula, CancellationToken cancellationToken)
    {
        var url = $"https://apis.gometa.org/cedulas/{cedula}";

        try
        {
            var http = _httpClientFactory.CreateClient(ExternalClientName);
            using var response = await http.GetAsync(url, cancellationToken);
            if (!response.IsSuccessStatusCode)
                return null;

            var responseBody = await response.Content.ReadAsStringAsync(cancellationToken);
            var parsed = JsonConvert.DeserializeObject<GoMetaResponse>(responseBody);
            if (parsed == null)
                return null;

            var first = parsed.Results?.FirstOrDefault();
            return new CedulaDatosDto
            {
                FullName = first?.FullName,
                FirstName = first?.FirstName,
                LastName = first?.LastName,
                RegimenDescripcion = parsed.Regimen?.Descripcion,
                TipoIdentificacion = parsed.TipoIdentificacion,
                SituacionEstado = parsed.Situacion?.Estado,
                TipoRegimen = first?.GuessType,
                Actividades = parsed.Actividades?.Select(a => new ActividadDto
                {
                    Codigo = a.Ciiu3?.Codigo ?? a.Codigo,
                    Descripcion = a.Descripcion,
                    CIIU4 = a.CIIU4,
                    CIIU4desc = a.CIIU4desc
                }).ToList() ?? []
            };
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Error al consultar GoMeta para cédula {Cedula}", cedula);
            return null;
        }
    }
}

