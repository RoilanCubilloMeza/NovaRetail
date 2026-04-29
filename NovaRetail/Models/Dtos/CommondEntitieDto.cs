using Newtonsoft.Json;

namespace NovaRetail.Models.Dtos;

public sealed class CommondEntitieDto
{
    [JsonProperty("intId")]
    public int IntId { get; set; }

    [JsonProperty("intCodigo")]
    public int IntCodigo { get; set; }

    [JsonProperty("strCodigo")]
    public string? StrCodigo { get; set; }

    [JsonProperty("strDescripcion")]
    public string? StrDescripcion { get; set; }
}
