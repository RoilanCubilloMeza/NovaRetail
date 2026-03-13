using Newtonsoft.Json;
using System.Globalization;
using System.Net.NetworkInformation;
using System.Text;
using System.Text.RegularExpressions;

namespace NovaRetail.Services
{
    public class Utilities
    {
        private static readonly HttpClient _http = new()
        {
            Timeout = TimeSpan.FromSeconds(20)
        };

        public string obtenerFechaActualConFormato()
        {
            return DateTime.Now.ToString("yyyy-MM-dd HH':'mm':'ss");
        }

        public string obtenerFechaActualConFormatoMas30Dias()
        {
            return DateTime.Now.AddDays(30).ToString("yyyy-MM-dd HH':'mm':'ss");
        }

        public string obtenerFechaActualParaReferencia()
        {
            return DateTime.Now.ToString("yyyyMMdd");
        }

        public bool validarEmail(string email)
        {
            const string expresion = "\\w+([-+.']\\w+)*@\\w+([-.]\\w+)*\\.\\w+([-.]\\w+)*";
            if (!Regex.IsMatch(email, expresion)) return false;
            return Regex.Replace(email, expresion, string.Empty).Length == 0;
        }

        public string GetDatosCedula(string cedula)
        {
            var url = $"https://apis.gometa.org/cedulas/{cedula}";

            try
            {
                using var response = _http.GetAsync(url).GetAwaiter().GetResult();
                if (!response.IsSuccessStatusCode)
                    return "Error";

                return response.Content.ReadAsStringAsync().GetAwaiter().GetResult();
            }
            catch
            {
                return "Error";
            }
        }

        public string ValidaExoneracion(string numero)
        {
            var url = $"https://api.hacienda.go.cr/fe/ex?autorizacion={numero}";

            try
            {
                using var response = _http.GetAsync(url).GetAwaiter().GetResult();
                if (!response.IsSuccessStatusCode)
                    return "Error";

                return response.Content.ReadAsStringAsync().GetAwaiter().GetResult();
            }
            catch
            {
                return "Error";
            }
        }

        public bool ValidarConexionInternet()
        {
            if (NetworkInterface.GetIsNetworkAvailable() is false)
                return false;

            try
            {
                using var cts = new CancellationTokenSource(TimeSpan.FromSeconds(5));
                using var request = new HttpRequestMessage(HttpMethod.Head, "https://www.google.com/");
                using var response = _http.Send(request, cts.Token);
                return response.IsSuccessStatusCode;
            }
            catch
            {
                return false;
            }
        }

        public string Base64Decode(string base64EncodedData)
        {
            var bytes = Convert.FromBase64String(base64EncodedData);
            return Encoding.UTF8.GetString(bytes);
        }

        public static string Base64Encode(string plainText)
        {
            var bytes = Encoding.UTF8.GetBytes(plainText);
            return Convert.ToBase64String(bytes);
        }

        public string RemoverTildes(string cadenaConTildes)
        {
            return Regex.Replace(cadenaConTildes.Normalize(NormalizationForm.FormD), @"[^a-zA-z0-9 ]+", "");
        }

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
            catch
            {
                return null;
            }
        }

        private static async Task<CedulaDatosDto?> GetDatosHaciendaAsync(string cedula, CancellationToken cancellationToken)
        {
            var url = $"https://api.hacienda.go.cr/fe/ae?identificacion={cedula}";

            try
            {
                using var response = await _http.GetAsync(url, cancellationToken);
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
                    }).ToList() ?? new List<ActividadDto>()
                };
            }
            catch
            {
                return null;
            }
        }

        private static async Task<CedulaDatosDto?> GetDatosGoMetaAsync(string cedula, CancellationToken cancellationToken)
        {
            var url = $"https://apis.gometa.org/cedulas/{cedula}";

            try
            {
                using var response = await _http.GetAsync(url, cancellationToken);
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
                    }).ToList() ?? new List<ActividadDto>()
                };
            }
            catch
            {
                return null;
            }
        }
    }

    public class CedulaDatosDto
    {
        public string? FullName { get; set; }
        public string? FirstName { get; set; }
        public string? LastName { get; set; }
        public string? RegimenDescripcion { get; set; }
        public string? TipoIdentificacion { get; set; }
        public string? SituacionEstado { get; set; }
        public string? TipoRegimen { get; set; }
        public List<ActividadDto> Actividades { get; set; } = new();
    }

    public class ActividadDto
    {
        public string? Codigo { get; set; }
        public string? Descripcion { get; set; }
        public string? CIIU4 { get; set; }
        public string? CIIU4desc { get; set; }
    }

    public class GoMetaResponse
    {
        [JsonProperty("results")]
        public List<GoMetaResult>? Results { get; set; }

        [JsonProperty("regimen")]
        public GoMetaRegimen? Regimen { get; set; }

        [JsonProperty("tipoIdentificacion")]
        public string? TipoIdentificacion { get; set; }

        [JsonProperty("situacion")]
        public GoMetaSituacion? Situacion { get; set; }

        [JsonProperty("actividades")]
        public List<GoMetaActividad>? Actividades { get; set; }
    }

    public class GoMetaResult
    {
        [JsonProperty("fullname")]
        public string? FullName { get; set; }

        [JsonProperty("firstname")]
        public string? FirstName { get; set; }

        [JsonProperty("lastname")]
        public string? LastName { get; set; }

        [JsonProperty("guess_type")]
        public string? GuessType { get; set; }
    }

    public class GoMetaRegimen
    {
        [JsonProperty("descripcion")]
        public string? Descripcion { get; set; }
    }

    public class GoMetaSituacion
    {
        [JsonProperty("estado")]
        public string? Estado { get; set; }
    }

    public class GoMetaActividad
    {
        [JsonProperty("codigo")]
        public string? Codigo { get; set; }

        [JsonProperty("descripcion")]
        public string? Descripcion { get; set; }

        [JsonProperty("CIIU4")]
        public string? CIIU4 { get; set; }

        [JsonProperty("CIIU4desc")]
        public string? CIIU4desc { get; set; }

        [JsonProperty("ciiu3")]
        public GoMetaCiiu3? Ciiu3 { get; set; }
    }

    public class GoMetaCiiu3
    {
        [JsonProperty("codigo")]
        public string? Codigo { get; set; }

        [JsonProperty("descripcion")]
        public string? Descripcion { get; set; }
    }

    public class HaciendaResponse
    {
        [JsonProperty("nombre")]
        public string? Nombre { get; set; }

        [JsonProperty("tipoIdentificacion")]
        public string? TipoIdentificacion { get; set; }

        [JsonProperty("regimen")]
        public GoMetaRegimen? Regimen { get; set; }

        [JsonProperty("situacion")]
        public GoMetaSituacion? Situacion { get; set; }

        [JsonProperty("actividades")]
        public List<HaciendaActividad>? Actividades { get; set; }
    }

    public class HaciendaActividad
    {
        [JsonProperty("codigo")]
        public string? Codigo { get; set; }

        [JsonProperty("descripcion")]
        public string? Descripcion { get; set; }
    }
}
