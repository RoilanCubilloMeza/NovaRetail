namespace NovaRetail.ViewModels
{
    public partial class MainViewModel
    {
        // Product catalog logic has been extracted to ProductCatalogViewModel.
        // These delegation methods are called by other partials (Checkout, Quotes, Cart).

        private Task ResetCatalogAfterCheckoutAsync()
            => ProductCatalog.ResetCatalogAfterCheckoutAsync();

        private bool IsNonInventoryItem(int itemType)
            => ProductCatalog.IsNonInventoryItem(itemType);
    }
}
