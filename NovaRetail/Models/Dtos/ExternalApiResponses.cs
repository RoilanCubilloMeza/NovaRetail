using Newtonsoft.Json;

namespace NovaRetail.Models.Dtos;

#region GoMeta API

/// <summary>Respuesta raíz del API de GoMeta (<c>https://apis.gometa.org/cedulas/{id}</c>).</summary>
public sealed class GoMetaResponse
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

public sealed class GoMetaResult
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

public sealed class GoMetaRegimen
{
    [JsonProperty("descripcion")]
    public string? Descripcion { get; set; }
}

public sealed class GoMetaSituacion
{
    [JsonProperty("estado")]
    public string? Estado { get; set; }
}

public sealed class GoMetaActividad
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

public sealed class GoMetaCiiu3
{
    [JsonProperty("codigo")]
    public string? Codigo { get; set; }

    [JsonProperty("descripcion")]
    public string? Descripcion { get; set; }
}

#endregion

#region Hacienda API

/// <summary>Respuesta raíz del API de Hacienda (<c>https://api.hacienda.go.cr/fe/ae</c>).</summary>
public sealed class HaciendaResponse
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

public sealed class HaciendaActividad
{
    [JsonProperty("codigo")]
    public string? Codigo { get; set; }

    [JsonProperty("descripcion")]
    public string? Descripcion { get; set; }
}

#endregion
