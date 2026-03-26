namespace NovaRetail.State;

/// <summary>
/// Record inmutable que representa el estado global de la UI.
/// Se modifica únicamente a través de <see cref="AppReducer.Reduce"/> al despachar acciones.
/// </summary>
public sealed record AppState(
    // ── UI overlays ──
    bool IsItemActionVisible = false,
    bool IsPriceJustVisible = false,
    bool IsDiscountPopupVisible = false,
    bool IsSelectionMode = false,
    bool IsCheckoutVisible = false,
    bool IsReceiptVisible = false,
    bool IsProductsPanelVisible = true,
    bool IsManualExonerationVisible = false,
    bool IsOrderSearchVisible = false,
    bool IsQuoteReceiptVisible = false,
    bool IsSalesRepPickerVisible = false,

    // ── Cliente ──
    string CurrentClientId = "",
    string CurrentClientName = "",
    bool IsCurrentClientReceiver = false,
    string CurrentClientCustomerType = "",

    // ── Carrito: ordenamiento ──
    string CartSortField = "",
    bool IsCartSortDescending = true,

    // ── Búsqueda de productos ──
    string ProductSearchText = "",
    string SelectedTab = "Rápido",
    string SelectedCategory = "Todos",

    // ── Descuento del ticket ──
    int DiscountPercent = 0);
