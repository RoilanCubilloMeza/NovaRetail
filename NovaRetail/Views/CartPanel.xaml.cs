using NovaRetail.Models;
using NovaRetail.ViewModels;

namespace NovaRetail.Views;

/// <summary>
/// Panel visual del carrito.
/// Además de presentar las líneas y totales, resuelve detalles de interacción propios de la UI,
/// como ocultar/mostrar el código del producto según ancho disponible y abrir edición por long press.
/// </summary>
public partial class CartPanel : ContentView
{
    public static readonly BindableProperty ShowProductCodeProperty = BindableProperty.Create(
        nameof(ShowProductCode),
        typeof(bool),
        typeof(CartPanel),
        false);

    private const double ProductCodeBreakpoint = 900;
    private CancellationTokenSource? _longPressCts;

    public bool ShowProductCode
    {
        get => (bool)GetValue(ShowProductCodeProperty);
        private set => SetValue(ShowProductCodeProperty, value);
    }

    public CartPanel()
    {
        InitializeComponent();
        SizeChanged += OnCartPanelSizeChanged;
    }

    /// <summary>
    /// Ajusta si el código del producto se muestra o no según el ancho actual del panel.
    /// Esto ayuda a mantener legible el carrito en resoluciones pequeñas.
    /// </summary>
    private void OnCartPanelSizeChanged(object? sender, EventArgs e)
    {
        ShowProductCode = Width >= ProductCodeBreakpoint;
    }

    /// <summary>
    /// Inicia un temporizador de long press sobre una línea del carrito.
    /// Si el usuario mantiene presionado el ítem, se abre el popup de edición.
    /// </summary>
    private void OnItemPointerPressed(object? sender, PointerEventArgs e)
    {
        _longPressCts?.Cancel();
        _longPressCts = new CancellationTokenSource();
        var cts = _longPressCts;

        var item = (sender as Element)?.BindingContext as CartItemModel;
        if (item is null) return;

        _ = Task.Run(async () =>
        {
            try
            {
                await Task.Delay(600, cts.Token);
                await MainThread.InvokeOnMainThreadAsync(() =>
                {
                    var vm = BindingContext as MainViewModel;
                    if (vm?.EditCartItemCommand.CanExecute(item) == true)
                        vm.EditCartItemCommand.Execute(item);
                });
            }
            catch (OperationCanceledException) { }
        });
    }

    /// <summary>
    /// Cancela el long press cuando el usuario suelta el puntero.
    /// Evita abrir la edición por toques cortos o desplazamientos accidentales.
    /// </summary>
    private void OnItemPointerReleased(object? sender, PointerEventArgs e)
    {
        _longPressCts?.Cancel();
    }
}
