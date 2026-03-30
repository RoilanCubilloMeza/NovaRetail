namespace NovaRetail;

public sealed record CategoryOption(string Key, string Label, string Icon, string? Seed = null)
{
    public string TabText => $"{Icon} {Label}";
}

/// <summary>
/// Claves de categoría usadas como identificadores en el ViewModel y la UI.
/// Centraliza los literales para evitar valores quemados dispersos.
/// </summary>
public static class CategoryKeys
{
    public const string Todos        = "Todos";
    public const string Supermercado = "Supermercado";
    public const string Super        = "Super";
    public const string Ferreteria   = "Ferreteria";
    public const string Calzado      = "Calzado";
    public const string Hogar        = "Hogar";

    public static readonly IReadOnlyList<CategoryOption> Options =
        new[]
        {
            new CategoryOption(Todos, "Todos", "🏷️"),
            new CategoryOption(Supermercado, "Supermercado", "🛒"),
            new CategoryOption(Ferreteria, "Ferretería", "🔧", "tornillo"),
            new CategoryOption(Calzado, "Calzado", "👟", "tenis"),
            new CategoryOption(Hogar, "Hogar", "🏠", "escoba"),
        };

    /// <summary>
    /// Término de búsqueda semilla que se usa para cargar productos por categoría
    /// desde la API cuando aún no se han cargado localmente.
    /// </summary>
    public static readonly IReadOnlyDictionary<string, string> Seeds =
        Options
            .Where(option => !string.IsNullOrWhiteSpace(option.Seed))
            .ToDictionary(option => option.Key, option => option.Seed!, StringComparer.OrdinalIgnoreCase);
}
