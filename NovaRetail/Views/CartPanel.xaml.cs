using NovaRetail.Models;
using NovaRetail.ViewModels;

namespace NovaRetail.Views;

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

    private void OnCartPanelSizeChanged(object? sender, EventArgs e)
    {
        ShowProductCode = Width >= ProductCodeBreakpoint;
    }

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

    private void OnItemPointerReleased(object? sender, PointerEventArgs e)
    {
        _longPressCts?.Cancel();
    }
}
