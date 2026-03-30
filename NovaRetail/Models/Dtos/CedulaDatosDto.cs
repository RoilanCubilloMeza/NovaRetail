namespace NovaRetail.Models.Dtos;

/// <summary>
/// Datos unificados obtenidos de fuentes externas (Hacienda, GoMeta)
/// para una cédula costarricense.
/// </summary>
public sealed class CedulaDatosDto
{
    public string? FullName { get; set; }
    public string? FirstName { get; set; }
    public string? LastName { get; set; }
    public string? RegimenDescripcion { get; set; }
    public string? TipoIdentificacion { get; set; }
    public string? SituacionEstado { get; set; }
    public string? TipoRegimen { get; set; }
    public List<ActividadDto> Actividades { get; set; } = [];
}

/// <summary>
/// Actividad económica normalizada proveniente de fuentes externas.
/// </summary>
public sealed class ActividadDto
{
    public string? Codigo { get; set; }
    public string? Descripcion { get; set; }
    public string? CIIU4 { get; set; }
    public string? CIIU4desc { get; set; }
}
