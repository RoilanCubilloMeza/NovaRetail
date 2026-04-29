namespace NovaRetail;

public sealed record TabOption(string Key, string Label, string Icon)
{
    public string TabText => $"{Icon} {Label}";
}

/// <summary>
/// Claves de tabs usadas como identificadores para el catálogo principal.
/// Centraliza los literales para evitar valores quemados en estado, ViewModel y UI.
/// </summary>
public static class TabKeys
{
    public const string Rapido = "Rápido";
    public const string Categorias = "Categorías";
    public const string Promos = "Promos";

    public static readonly IReadOnlyList<TabOption> Options =
        new[]
        {
            new TabOption(Rapido, "Rápido", "⚡"),
            new TabOption(Categorias, "Categorías", "📂"),
            new TabOption(Promos, "Promos", "🏷️"),
        };
}