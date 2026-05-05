namespace NovaRetail.ViewModels
{
    public partial class MainViewModel
    {
        // Product catalog logic has been extracted to ProductCatalogViewModel.
        // These delegation methods are called by other partials (Checkout, Quotes, Cart).

        private Task ResetCatalogAfterCheckoutAsync(IReadOnlyCollection<NovaRetail.Models.CartItemModel>? completedCartItems = null)
            => ProductCatalog.ResetCatalogAfterCheckoutAsync(completedCartItems);

        private bool IsNonInventoryItem(int itemType)
            => ProductCatalog.IsNonInventoryItem(itemType);

        private bool CanAddWithoutInventory
            => _allowInvoiceWithoutInventory || _allowOrderWithoutInventory;

        private async Task RefreshInventoryPermissionConfigAsync()
        {
            try
            {
                var config = await _storeConfigService.GetConfigAsync();
                _allowInvoiceWithoutInventory = config.AllowInvoiceWithoutInventory;
                _allowOrderWithoutInventory = config.AllowOrderWithoutInventory;
                ProductCatalog.SetStoreConfig(config.StoreID, ProductCatalogViewModel.ParseNonInventoryItemTypes(config.NonInventoryItemTypes));
                await ApplyInventoryPermissionParametersAsync();
            }
            catch
            {
            }
        }

        private async Task ApplyInventoryPermissionParametersAsync()
        {
            try
            {
                var parametros = await _parametrosService.GetParametrosAsync();
                foreach (var parametro in parametros)
                {
                    var codigo = (parametro.Codigo ?? string.Empty).Trim();
                    var enabled = string.Equals((parametro.Valor ?? string.Empty).Trim(), "1", StringComparison.OrdinalIgnoreCase);

                    if (codigo.StartsWith("factura_permite_sin_inventario", StringComparison.OrdinalIgnoreCase) ||
                        codigo.StartsWith("factura_permite", StringComparison.OrdinalIgnoreCase))
                    {
                        _allowInvoiceWithoutInventory = enabled;
                    }

                    if (codigo.StartsWith("orden_permite_sin_inventario", StringComparison.OrdinalIgnoreCase) ||
                        codigo.StartsWith("orden_permite", StringComparison.OrdinalIgnoreCase))
                    {
                        _allowOrderWithoutInventory = enabled;
                    }
                }

                System.Diagnostics.Debug.WriteLine(
                    $"[InventoryPermissions] factura={_allowInvoiceWithoutInventory}, orden={_allowOrderWithoutInventory}");
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"[InventoryPermissions] No se pudieron leer AVS_Parametros: {ex.Message}");
            }
        }
    }
}
