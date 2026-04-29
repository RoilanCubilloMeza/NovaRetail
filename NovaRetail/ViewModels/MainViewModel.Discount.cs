using NovaRetail.Models;

namespace NovaRetail.ViewModels
{
    public partial class MainViewModel
    {
        private async Task OpenItemActionAsync(CartItemModel? item)
        {
            if (item is null) return;

            if (_cachedDiscountCodes.Count == 0)
                await LoadDiscountCodesAsync();

            ItemActionVm.LoadItem(item, _cachedDiscountCodes, IsTaxIncluded);
            IsItemActionVisible = true;
        }

        private void OnItemActionOk()
        {
            if (ItemActionVm.IsServiceMode)
            {
                FinalizeServicePriceEntry();
                return;
            }
            CloseItemAction();
        }

        private void OnItemActionCancel()
        {
            _pendingServiceProduct = null;
            CloseItemAction();
        }

        private void CloseItemAction()
        {
            IsItemActionVisible = false;
            RecalculateTotal();
            RefreshCartItemsView();
        }

        private async Task ApplyDiscountAsync()
        {
            var result = await _dialogService.PromptAsync("Descuento", "Ingrese el porcentaje de descuento:",
                accept: "Aplicar",
                cancel: "Cancelar",
                maxLength: 3,
                keyboard: Keyboard.Numeric,
                initialValue: DiscountPercent.ToString());

            if (result is not null && int.TryParse(result, out var percent))
            {
                DiscountPercent = percent;
                RecalculateTotal();
            }
        }

        private async Task StartItemDiscountAsync()
        {
            var item = ItemActionVm.CurrentItem;
            if (item is null)
                return;

            if (_cachedDiscountCodes.Count == 0)
                await LoadDiscountCodesAsync();

            ItemActionVm.ApplyNonPriceChanges();
            _pendingPriceItem = item;
            _pendingDiscountPercent = null;
            _isDiscountJustificationFlow = false;
            DiscountVm.LoadPercent(item.DiscountPercent);
            IsItemActionVisible = false;
            IsDiscountPopupVisible = true;
            RecalculateTotal();
        }

        private void OnDiscountEntryOk()
        {
            var selectedPercent = DiscountVm.SelectedPercent;
            if (!selectedPercent.HasValue)
                return;

            _pendingDiscountPercent = selectedPercent.Value;
            IsDiscountPopupVisible = false;

            if (_isBulkDiscountFlow)
            {
                PriceJustVm.LoadCodes(_cachedDiscountCodes);
                IsPriceJustVisible = true;
                return;
            }

            if (_pendingPriceItem is null)
                return;

            _isDiscountJustificationFlow = true;
            PriceJustVm.LoadCodes(_cachedDiscountCodes);
            IsPriceJustVisible = true;
        }

        private void OnDiscountEntryCancel()
        {
            _pendingDiscountPercent = null;
            _isDiscountJustificationFlow = false;
            var reopenItemAction = !_isBulkDiscountFlow && _pendingPriceItem != null;
            _isBulkDiscountFlow = false;
            IsDiscountPopupVisible = false;
            if (reopenItemAction)
                IsItemActionVisible = true;
            _pendingPriceItem = null;
        }

        private void OnPriceJustificationRequired()
        {
            var item = ItemActionVm.CurrentItem;
            if (item is null) return;

            _pendingPriceItem = item;
            _pendingDiscountPercent = null;
            _isDiscountJustificationFlow = false;
            IsItemActionVisible = false;

            if (_cachedDiscountCodes.Count == 0)
                _ = LoadDiscountCodesAsync().ContinueWith(_ =>
                    MainThread.BeginInvokeOnMainThread(() =>
                    {
                        PriceJustVm.LoadCodes(_cachedDiscountCodes);
                        IsPriceJustVisible = true;
                    }));
            else
            {
                PriceJustVm.LoadCodes(_cachedDiscountCodes);
                IsPriceJustVisible = true;
            }
        }

        private void OnPriceJustOk()
        {
            if (PriceJustVm.SelectedCode is not null)
            {
                if (_isBulkDiscountFlow)
                {
                    if (_pendingDiscountPercent.HasValue)
                    {
                        var pct  = _pendingDiscountPercent.Value;
                        var code = PriceJustVm.SelectedCode.Code;
                        var codeId = PriceJustVm.SelectedCode.ID;
                        foreach (var item in CartItems.Where(c => c.IsSelected).ToList())
                        {
                            item.DiscountPercent = pct;
                            item.DiscountReasonCode = code;
                            item.DiscountReasonCodeID = codeId;
                        }
                    }
                    _isBulkDiscountFlow = false;
                    IsSelectionMode = false;
                }
                else if (_pendingPriceItem is not null)
                {
                    if (_isDiscountJustificationFlow)
                    {
                        if (_pendingDiscountPercent.HasValue)
                            _pendingPriceItem.DiscountPercent = _pendingDiscountPercent.Value;
                        _pendingPriceItem.DiscountReasonCode = PriceJustVm.SelectedCode.Code;
                        _pendingPriceItem.DiscountReasonCodeID = PriceJustVm.SelectedCode.ID;
                    }
                    else
                    {
                        var newPrice = ItemActionVm.PendingPriceColones;
                        if (newPrice.HasValue)
                        {
                            _pendingPriceItem.OverridePriceColones = newPrice;
                            _pendingPriceItem.DiscountReasonCode = PriceJustVm.SelectedCode.Code;
                            _pendingPriceItem.DiscountReasonCodeID = PriceJustVm.SelectedCode.ID;
                        }
                    }
                }
            }
            _pendingPriceItem = null;
            _pendingDiscountPercent = null;
            _isDiscountJustificationFlow = false;
            _isBulkDiscountFlow = false;
            IsPriceJustVisible = false;
            RecalculateTotal();
            RefreshCartItemsView();
        }

        private void OnPriceJustCancel()
        {
            _pendingPriceItem = null;
            _pendingDiscountPercent = null;
            var reopenItemAction = _isDiscountJustificationFlow && !_isBulkDiscountFlow;
            _isDiscountJustificationFlow = false;
            _isBulkDiscountFlow = false;
            IsPriceJustVisible = false;
            if (reopenItemAction)
                IsItemActionVisible = true;
            RecalculateTotal();
        }

        private async Task StartBulkDiscountAsync()
        {
            if (_cachedDiscountCodes.Count == 0)
                await LoadDiscountCodesAsync();

            _pendingPriceItem = null;
            _pendingDiscountPercent = null;
            _isBulkDiscountFlow = true;
            _isDiscountJustificationFlow = false;
            var firstSelected = CartItems.FirstOrDefault(c => c.IsSelected);
            DiscountVm.LoadPercent(firstSelected?.DiscountPercent ?? 0);
            IsDiscountPopupVisible = true;
        }

        private async Task LoadDiscountCodesAsync()
        {
            try
            {
                var codes = await _productService.GetReasonCodesAsync(4);
                if (codes.Count > 0)
                {
                    _cachedDiscountCodes.Clear();
                    _cachedDiscountCodes.AddRange(codes);
                }
            }
            catch
            {
            }
        }

        private int ResolveDiscountReasonCodeID(CartItemModel item)
        {
            if (!item.HasDiscount && (!item.HasOverridePrice || item.IsUpwardPriceOverride))
                return 0;

            if (item.DiscountReasonCodeID > 0)
                return item.DiscountReasonCodeID;

            if (!string.IsNullOrWhiteSpace(item.DiscountReasonCode))
            {
                var match = _cachedDiscountCodes.FirstOrDefault(c =>
                    string.Equals(c.Code, item.DiscountReasonCode, StringComparison.OrdinalIgnoreCase));
                if (match is not null)
                    return match.ID;
            }

            return _cachedDiscountCodes.FirstOrDefault()?.ID ?? 0;
        }
    }
}
