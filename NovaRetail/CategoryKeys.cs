using NovaRetail.Models;

namespace NovaRetail;

public sealed record CategoryOption(string Key, string Label, string Icon, int DepartmentID = 0)
{
    public string TabText => $"{Icon} {Label}";
}

/// <summary>
/// Claves de categoría usadas como identificadores en el ViewModel y la UI.
/// La opción "Todos" siempre está disponible; las demás se cargan dinámicamente
/// desde la tabla Department vía la API.
/// </summary>
public static class CategoryKeys
{
    public const string Todos = "Todos";

    private static readonly object _lock = new();
    private static List<CategoryOption> _dynamicOptions = new() { new(Todos, "Todos", "📋") };

    /// <summary>Opciones activas (Todos + categorías cargadas de la DB).</summary>
    public static IReadOnlyList<CategoryOption> Options
    {
        get { lock (_lock) return _dynamicOptions.ToList(); }
    }
    /// <summary>Obtiene el DepartmentID de una categoría por nombre.</summary>
    public static int GetDepartmentID(string category)
    {
        lock (_lock)
        {
            var option = _dynamicOptions.FirstOrDefault(o =>
                string.Equals(o.Key, category, StringComparison.OrdinalIgnoreCase));
            return option?.DepartmentID ?? 0;
        }
    }
    /// <summary>
    /// Reemplaza las categorías dinámicas con las obtenidas del API.
    /// Debe llamarse una vez después de obtener las categorías de la DB.
    /// </summary>
    public static void Load(IEnumerable<CategoryModel> categories)
    {
        var list = new List<CategoryOption> { new(Todos, "Todos", "📋") };

        foreach (var cat in categories)
        {
            if (string.IsNullOrWhiteSpace(cat.Name))
                continue;

            list.Add(new CategoryOption(cat.Name, cat.Name, "\U0001f4c2", cat.ID));
        }

        lock (_lock)
            _dynamicOptions = list;
    }

    /// <summary>Comprueba si un nombre de categoría es conocido.</summary>
    public static bool IsKnown(string category)
    {
        if (string.Equals(category, Todos, StringComparison.OrdinalIgnoreCase))
            return true;

        lock (_lock)
            return _dynamicOptions.Any(o => string.Equals(o.Key, category, StringComparison.OrdinalIgnoreCase));
    }
}
