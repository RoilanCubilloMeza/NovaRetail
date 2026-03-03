namespace NovaRetail.Controls
{
    /// <summary>
    /// Label preconfigurado para iconos Unicode en formularios.
    /// </summary>
    public class IconLabel : Label
    {
        public IconLabel()
        {
            FontSize        = 14;
            TextColor       = UiConfig.TextGray500;
            VerticalOptions = LayoutOptions.Center;
        }
    }
}
