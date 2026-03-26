namespace NovaRetail.Controls;

/// <summary>
/// Línea separadora reutilizable para formularios.
/// </summary>
public class FormDivider : BoxView
{
    public FormDivider()
    {
        Color = UiConfig.BorderGray;
        HeightRequest = 1;
    }
}
