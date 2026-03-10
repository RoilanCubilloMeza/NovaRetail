namespace NovaRetail.State
{
    public sealed record AppState(
        // ── UI overlays ──
        bool IsItemActionVisible = false,
        bool IsPriceJustVisible = false,
        bool IsDiscountPopupVisible = false,
        bool IsSelectionMode = false,
        bool IsCheckoutVisible = false,
        bool IsProductsPanelVisible = true,

        // ── Cliente ──
        string CurrentClientId = "",
        string CurrentClientName = "",

        // ── Carrito: ordenamiento ──
        string CartSortField = "",
        bool IsCartSortDescending = true,

        // ── Búsqueda de productos ──
        string ProductSearchText = "",
        string SelectedTab = "Rápido",
        string SelectedCategory = "Todos",

        // ── Descuento del ticket ──
        int DiscountPercent = 0);
}
