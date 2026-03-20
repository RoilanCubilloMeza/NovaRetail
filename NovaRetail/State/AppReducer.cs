using System;

namespace NovaRetail.State
{
    public static class AppReducer
    {
        public static AppState Reduce(AppState state, IAppAction action) => action switch
        {
            // ── UI overlays ──
            SetItemActionVisibleAction a => state with { IsItemActionVisible = a.Value },
            SetPriceJustVisibleAction a => state with { IsPriceJustVisible = a.Value },
            SetDiscountPopupVisibleAction a => state with { IsDiscountPopupVisible = a.Value },
            SetSelectionModeAction a => state with { IsSelectionMode = a.Value },
            SetCheckoutVisibleAction a => state with { IsCheckoutVisible = a.Value },
            SetReceiptVisibleAction a => state with { IsReceiptVisible = a.Value },
            SetProductsPanelVisibleAction a => state with { IsProductsPanelVisible = a.Value },
            SetManualExonerationVisibleAction a => state with { IsManualExonerationVisible = a.Value },

            // ── Cliente ──
            SetCurrentClientAction a => state with
            {
                CurrentClientId = a.ClientId ?? string.Empty,
                CurrentClientName = a.ClientName ?? string.Empty,
                IsCurrentClientReceiver = a.IsReceiver
            },

            // ── Carrito: ordenamiento ──
            SetCartSortFieldAction a => state with { CartSortField = a.Field ?? string.Empty },
            SetCartSortDescendingAction a => state with { IsCartSortDescending = a.Value },

            // ── Búsqueda de productos ──
            SetProductSearchTextAction a => state with { ProductSearchText = a.Text ?? string.Empty },
            SetSelectedTabAction a => state with { SelectedTab = a.Tab ?? "Rápido" },
            SetSelectedCategoryAction a => state with { SelectedCategory = a.Category ?? "Todos" },

            // ── Descuento del ticket ──
            SetDiscountPercentAction a => state with { DiscountPercent = Math.Clamp(a.Percent, 0, 100) },

            _ => state
        };
    }
}
