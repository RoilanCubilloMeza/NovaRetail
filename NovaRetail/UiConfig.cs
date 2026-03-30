namespace NovaRetail;

/// <summary>
/// Design tokens centralizados para los controles C#.
/// Cambiar un valor aquí se refleja en todos los controles que lo referencien.
/// </summary>
public static class UiConfig
{
    // ─── Colores de texto ───

    public static readonly Color TextPrimary      = Color.FromArgb("#212121");
    public static readonly Color TextSecondary     = Color.FromArgb("#919191");
    public static readonly Color TextGray500       = Color.FromArgb("#6B7280");
    public static readonly Color TextGray600       = Color.FromArgb("#404040");
    public static readonly Color TextDarkBlue      = Color.FromArgb("#1E3A5F");
    public static readonly Color PlaceholderColor  = Color.FromArgb("#ACACAC");

    // ─── Fondos y bordes ───

    public static readonly Color InputBackground   = Color.FromArgb("#F3F4F6");
    public static readonly Color BorderGray        = Color.FromArgb("#C8C8C8");
    public static readonly Color BorderLight       = Color.FromArgb("#E1E1E1");
    public static readonly Color GreenSurface      = Color.FromArgb("#EDFDF4");

    // ─── Acentos ───

    public static readonly Color AccentGreen       = Color.FromArgb("#22C55E");
    public static readonly Color AccentGreenDark   = Color.FromArgb("#1DA44E");
    public static readonly Color AccentBlue        = Color.FromArgb("#3B82F6");
    public static readonly Color AccentOrange      = Color.FromArgb("#EA580C");
    public static readonly Color ErrorRed          = Color.FromArgb("#DC2626");

    // ─── Otros ───

    public static readonly Color ShadowColor       = Color.FromArgb("#000000");
    public static readonly Color SwitchThumbOff    = Color.FromArgb("#6E6E6E");

    // ─── Radios de esquina ───

    public static readonly double CornerRadiusSm   = 6;
    public static readonly double CornerRadiusMd   = 8;
    public static readonly double CornerRadiusLg   = 10;
    public static readonly double CornerRadiusXl   = 12;

    // ─── Inputs ───

    public static readonly double InputHeight      = 40;
    public static readonly Thickness InputPadding   = new(10, 0);
    public static readonly double StrokeThin       = 1;

    // ─── Moneda regional ───

    /// <summary>
    /// Símbolo de moneda según la configuración regional de Windows (intl.cpl → Moneda).
    /// Lee directamente del registro para reflejar cambios sin reiniciar la app.
    /// </summary>
    public static string CurrencySymbol
    {
        get
        {
            try
            {
#if WINDOWS
                return Microsoft.Win32.Registry.GetValue(
                    @"HKEY_CURRENT_USER\Control Panel\International",
                    "sCurrency", "₡")?.ToString() ?? "₡";
#else
                return System.Globalization.CultureInfo.CurrentCulture.NumberFormat.CurrencySymbol;
#endif
            }
            catch
            {
                return "₡";
            }
        }
    }

    // ─── Sombra de tarjeta (nueva instancia por control) ───

    public static Shadow CardShadow() => new()
    {
        Brush   = new SolidColorBrush(ShadowColor),
        Offset  = new Point(0, 2),
        Radius  = 8,
        Opacity = 0.07f
    };
}
