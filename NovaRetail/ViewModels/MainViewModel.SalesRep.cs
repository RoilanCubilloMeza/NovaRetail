using NovaRetail.Models;

namespace NovaRetail.ViewModels
{
    public partial class MainViewModel
    {
        private async Task ShowSalesRepPickerAsync()
        {
            try
            {
                if (_cachedSalesReps.Count == 0)
                {
                    var reps = await _salesRepService.GetAllAsync();
                    _cachedSalesReps.Clear();
                    _cachedSalesReps.AddRange(reps);
                }

                if (_cachedSalesReps.Count == 0)
                    return;

                SalesRepPickerVm.Load(
                    _cachedSalesReps,
                    canSkip: true,
                    title: "Seleccionar Vendedor",
                    subtitle: "Busque y seleccione el vendedor para esta sesión.");
                IsSalesRepPickerVisible = true;
            }
            catch
            {
            }
        }

        public async Task ShowSalesRepPickerForItemsAsync()
        {
            var selectedItems = CartItems.Where(c => c.IsSelected).ToList();
            var subtitle = selectedItems.Count > 0
                ? $"Se asignará a {selectedItems.Count} artículo(s) seleccionado(s)."
                : "Se asignará a todos los artículos del carrito sin vendedor.";

            _salesRepPickerContext = SalesRepPickerContext.BulkCart;
            _pendingRepItem = null;
            await OpenSalesRepPickerAsync("Asignar Vendedor", subtitle, canSkip: true);
        }

        private void OnSalesRepSelected(SalesRepModel rep)
        {
            switch (_salesRepPickerContext)
            {
                case SalesRepPickerContext.SingleItem:
                    if (_pendingRepItem is not null)
                    {
                        _pendingRepItem.SalesRepID   = rep.ID;
                        _pendingRepItem.SalesRepName = rep.Nombre;
                    }
                    ItemActionVm.RefreshSalesRep(rep);
                    break;

                case SalesRepPickerContext.Checkout:
                    _activeSalesRep = rep;
                    OnPropertyChanged(nameof(ActiveSalesRepName));
                    OnPropertyChanged(nameof(HasActiveSalesRep));
                    CheckoutVm.SetSalesRep(rep);
                    foreach (var item in CartItems)
                    {
                        item.SalesRepID   = rep.ID;
                        item.SalesRepName = rep.Nombre;
                    }
                    break;

                case SalesRepPickerContext.BeforeCheckout:
                    _activeSalesRep = rep;
                    OnPropertyChanged(nameof(ActiveSalesRepName));
                    OnPropertyChanged(nameof(HasActiveSalesRep));
                    // Asignar solo a los artículos que no tienen vendedor aún
                    foreach (var item in CartItems.Where(c => c.SalesRepID == 0))
                    {
                        item.SalesRepID   = rep.ID;
                        item.SalesRepName = rep.Nombre;
                    }
                    _pendingRepItem = null;
                    IsSalesRepPickerVisible = false;
                    RefreshCartItemsView();
                    OpenCheckoutPopup();
                    return;

                default: // Session / BulkCart
                    _activeSalesRep = rep;
                    OnPropertyChanged(nameof(ActiveSalesRepName));
                    OnPropertyChanged(nameof(HasActiveSalesRep));
                    var targets = CartItems.Where(c => c.IsSelected).ToList();
                    if (targets.Count == 0)
                        targets = CartItems.Where(c => c.SalesRepID == 0).ToList();
                    foreach (var item in targets)
                    {
                        item.SalesRepID   = rep.ID;
                        item.SalesRepName = rep.Nombre;
                    }
                    break;
            }

            _pendingRepItem = null;
            IsSalesRepPickerVisible = false;
            RefreshCartItemsView();
        }

        private void OpenSalesRepPickerForItem(CartItemModel? item)
        {
            if (item is null) return;
            _salesRepPickerContext = SalesRepPickerContext.SingleItem;
            _pendingRepItem = item;
            _ = OpenSalesRepPickerAsync(
                title: "Vendedor del Artículo",
                subtitle: $"Asignar vendedor a: {item.DisplayName}",
                canSkip: true);
        }

        private void OpenSalesRepPickerForCheckout()
        {
            _salesRepPickerContext = SalesRepPickerContext.Checkout;
            _pendingRepItem = null;
            _ = OpenSalesRepPickerAsync(
                title: "Vendedor de la Venta",
                subtitle: "Se asignará a todos los artículos del carrito.",
                canSkip: true);
        }

        private async Task OpenSalesRepPickerAsync(string title, string subtitle, bool canSkip)
        {
            try
            {
                if (_cachedSalesReps.Count == 0)
                {
                    var reps = await _salesRepService.GetAllAsync();
                    _cachedSalesReps.Clear();
                    _cachedSalesReps.AddRange(reps);
                }

                if (_cachedSalesReps.Count == 0)
                {
                    await _dialogService.AlertAsync("Vendedor", "No se encontraron vendedores configurados.", "OK");
                    return;
                }

                SalesRepPickerVm.Load(_cachedSalesReps, canSkip: canSkip, title: title, subtitle: subtitle);
                IsSalesRepPickerVisible = true;
            }
            catch { }
        }

        private void OnSalesRepSkipped()
        {
            var ctx = _salesRepPickerContext;
            IsSalesRepPickerVisible = false;

            // Si el picker se mostró justo antes del checkout, continuar con él
            if (ctx == SalesRepPickerContext.BeforeCheckout)
                OpenCheckoutPopup();
        }
    }
}
