namespace NovaRetail.State
{
    public interface IAppAction;

    // ── UI overlays ──
    public sealed record SetItemActionVisibleAction(bool Value) : IAppAction;
    public sealed record SetPriceJustVisibleAction(bool Value) : IAppAction;
    public sealed record SetDiscountPopupVisibleAction(bool Value) : IAppAction;
    public sealed record SetSelectionModeAction(bool Value) : IAppAction;
    public sealed record SetCheckoutVisibleAction(bool Value) : IAppAction;
    public sealed record SetReceiptVisibleAction(bool Value) : IAppAction;
    public sealed record SetProductsPanelVisibleAction(bool Value) : IAppAction;

    // ── Cliente ──
    public sealed record SetCurrentClientAction(string ClientId, string ClientName) : IAppAction;

    // ── Carrito: ordenamiento ──
    public sealed record SetCartSortFieldAction(string Field) : IAppAction;
    public sealed record SetCartSortDescendingAction(bool Value) : IAppAction;

    // ── Búsqueda de productos ──
    public sealed record SetProductSearchTextAction(string Text) : IAppAction;
    public sealed record SetSelectedTabAction(string Tab) : IAppAction;
    public sealed record SetSelectedCategoryAction(string Category) : IAppAction;

    // ── Descuento del ticket ──
    public sealed record SetDiscountPercentAction(int Percent) : IAppAction;
}
