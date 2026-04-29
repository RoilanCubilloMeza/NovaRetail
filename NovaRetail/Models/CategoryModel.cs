namespace NovaRetail.Models;

/// <summary>Departamento/categoría cargado dinámicamente desde la API.</summary>
public class CategoryModel
{
    public int ID { get; set; }
    public string Name { get; set; } = string.Empty;
}
