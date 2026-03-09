using NovaRetail.Models;
using NovaRetail.ViewModels;

namespace NovaRetail.Views
{
    public partial class CartPanel : ContentView
    {
        private CancellationTokenSource? _longPressCts;

        public CartPanel()
        {
            InitializeComponent();
        }

        private void OnItemPointerPressed(object sender, PointerEventArgs e)
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

        private void OnItemPointerReleased(object sender, PointerEventArgs e)
        {
            _longPressCts?.Cancel();
        }
    }
}
