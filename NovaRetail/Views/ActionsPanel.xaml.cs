namespace NovaRetail.Views;

/// <summary>
/// Panel lateral de acciones rápidas del POS.
/// Su XAML contiene accesos como facturar, cotizar, limpiar carrito o buscar órdenes.
/// El code-behind es mínimo porque la lógica vive en el ViewModel principal.
/// </summary>
public partial class ActionsPanel : ContentView
{
    public ActionsPanel()
    {
        InitializeComponent();
    }
}
