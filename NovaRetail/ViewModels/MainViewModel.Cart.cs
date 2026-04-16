using NovaRetail.Models;

namespace NovaRetail.ViewModels
{
    public partial class MainViewModel
    {
        private void AddProduct(ProductModel? product)
            => AddProduct(product, 1m);

        private Task AddProductAsync(ProductModel? product)
        {
            if (product is null) return Task.CompletedTask;

            if (IsNonInventoryItem(product.ItemType))
            {
                OpenServicePriceEntry(product);
                return Task.CompletedTask;
            }

            AddProduct(product, 1m);
            return Task.CompletedTask;
        }

        /// <summary>
        /// Opens the ItemAction popup in service-price mode so the user
        /// can type the price using the existing numeric keypad.
        /// </summary>
        private void OpenServicePriceEntry(ProductModel product)
        {
            _pendingServiceProduct = product;

            var existing = CartItems.FirstOrDefault(c =>
                c.ItemID == product.ItemID &&
                string.Equals(c.Code, product.Code, StringComparison.OrdinalIgnoreCase));

            var prefillPrice = existing?.UnitPriceColones ??
                               (product.PriceColonesValue > 0 ? product.PriceColonesValue : 0m);

            ItemActionVm.LoadServiceItem(product, prefillPrice, IsTaxIncluded);
            IsItemActionVisible = true;
        }

        /// <summary>
        /// Called when the user presses "Aplicar" in service-price mode.
        /// Reads the price from the keypad and creates/updates the cart item.
        /// </summary>
        private async void FinalizeServicePriceEntry()
        {
            var product = _pendingServiceProduct;
            _pendingServiceProduct = null;

            var priceColones = ItemActionVm.ServicePriceColones;
            IsItemActionVisible = false;

            if (product is null || priceColones is null || priceColones <= 0m)
            {
                await _dialogService.AlertAsync(
                    "Precio inválido",
                    "Debe ingresar un valor numérico mayor a cero.",
                    "OK");
                return;
            }

            var priceDollars = _exchangeRate > 0
                ? Math.Round(priceColones.Value / _exchangeRate, 2)
                : priceColones.Value;

            var existing = CartItems.FirstOrDefault(c =>
                c.ItemID == product.ItemID &&
                string.Equals(c.Code, product.Code, StringComparison.OrdinalIgnoreCase));

            if (existing is not null)
            {
                existing.UnitPriceColones = priceColones.Value;
                existing.UnitPrice = priceDollars;
                existing.Quantity += 1m;
                existing.NotifyPriceChanged();
                product.CartQuantity = existing.Quantity;
            }
            else
            {
                var newItem = new CartItemModel
                {
                    ItemID = product.ItemID,
                    Emoji = product.Emoji,
                    Name = product.Name,
                    Code = product.Code,
                    UnitPrice = priceDollars,
                    UnitPriceColones = priceColones.Value,
                    TaxPercentage = product.TaxPercentage,
                    TaxID = product.TaxId,
                    Cabys = product.Cabys,
                    Stock = product.Stock,
                    ItemType = product.ItemType,
                    Quantity = 1m,
                    SalesRepID = _activeSalesRep?.ID ?? 0,
                    SalesRepName = _activeSalesRep?.Nombre ?? string.Empty
                };
                CartItems.Insert(0, newItem);
                product.CartQuantity = 1m;
                UpdateExonerationEligibility(newItem, null);
            }

            RecalculateTotal();
            RefreshCartItemsView();
        }

        private void AddProduct(ProductModel? product, decimal quantityToAdd)
        {
            if (product is null) return;

            var isNonInventory = IsNonInventoryItem(product.ItemType);
            var safeQuantity = quantityToAdd <= 0m ? 1m : Math.Floor(quantityToAdd);
            var existing = CartItems.FirstOrDefault(c => c.ItemID == product.ItemID && string.Equals(c.Code, product.Code, StringComparison.OrdinalIgnoreCase));

            decimal quantityToApply;
            if (isNonInventory)
            {
                quantityToApply = safeQuantity;
            }
            else
            {
                var maxAvailableQuantity = GetMaxAvailableQuantity(product.Stock);
                var currentQuantity = existing?.Quantity ?? 0m;
                var availableToAdd = maxAvailableQuantity - currentQuantity;

                if (availableToAdd <= 0m)
                {
                    ShowStockLimitAlert(product.Name, maxAvailableQuantity);
                    return;
                }

                quantityToApply = Math.Min(safeQuantity, availableToAdd);
            }

            if (existing is not null)
            {
                existing.Quantity += quantityToApply;
                product.CartQuantity = existing.Quantity;
            }
            else
            {
                var newItem = new CartItemModel
                {
                    ItemID = product.ItemID,
                    Emoji = product.Emoji,
                    Name = product.Name,
                    Code = product.Code,
                    UnitPrice = product.PriceValue,
                    UnitPriceColones = product.PriceColonesValue,
                    TaxPercentage = product.TaxPercentage,
                    TaxID = product.TaxId,
                    Cabys = product.Cabys,
                    Stock = product.Stock,
                    ItemType = product.ItemType,
                    Quantity = quantityToApply,
                    SalesRepID = _activeSalesRep?.ID ?? 0,
                    SalesRepName = _activeSalesRep?.Nombre ?? string.Empty
                };
                CartItems.Insert(0, newItem);
                product.CartQuantity = quantityToApply;
                UpdateExonerationEligibility(newItem, null);
            }

            if (!isNonInventory && quantityToApply < safeQuantity)
                ShowStockLimitAlert(product.Name, GetMaxAvailableQuantity(product.Stock));

            RecalculateTotal();
            RefreshCartItemsView();
        }

        private async Task AddManualItemAsync()
        {
            var name = await _dialogService.PromptAsync(
                "Artículo manual",
                "Ingrese la descripción del artículo.",
                accept: "Siguiente",
                cancel: "Cancelar",
                placeholder: "Descripción");

            if (string.IsNullOrWhiteSpace(name))
                return;

            var priceText = await _dialogService.PromptAsync(
                "Artículo manual",
                "Ingrese el precio en colones.",
                accept: "Siguiente",
                cancel: "Cancelar",
                placeholder: "Ej. 1500",
                keyboard: Keyboard.Numeric);

            if (string.IsNullOrWhiteSpace(priceText) ||
                !TryParseDecimal(priceText, out var priceColones) ||
                priceColones <= 0)
            {
                await _dialogService.AlertAsync("Artículo manual", "Ingrese un precio válido mayor a cero.", "OK");
                return;
            }

            var quantityText = await _dialogService.PromptAsync(
                "Artículo manual",
                "Ingrese la cantidad.",
                accept: "Agregar",
                cancel: "Cancelar",
                placeholder: "Ej. 1",
                keyboard: Keyboard.Numeric,
                initialValue: "1");

            if (quantityText is null)
                return;

            var quantity = 1m;
            if (!string.IsNullOrWhiteSpace(quantityText) &&
                (!TryParseDecimal(quantityText, out quantity) || quantity <= 0))
            {
                await _dialogService.AlertAsync("Artículo manual", "Ingrese una cantidad válida mayor a cero.", "OK");
                return;
            }

            var roundedPriceColones = Math.Round(priceColones, 2);
            var item = new CartItemModel
            {
                ItemID = 0,
                Emoji = "📝",
                Name = name.Trim(),
                Code = $"MANUAL-{DateTime.Now:HHmmss}",
                UnitPriceColones = roundedPriceColones,
                UnitPrice = ConvertFromColones(roundedPriceColones),
                TaxPercentage = _defaultTaxPercentage,
                TaxID = 0,
                Cabys = string.Empty,
                Stock = quantity,
                Quantity = quantity,
                SalesRepID = _activeSalesRep?.ID ?? 0,
                SalesRepName = _activeSalesRep?.Nombre ?? string.Empty
            };

            CartItems.Insert(0, item);
            UpdateExonerationEligibility(item, null);
            RecalculateTotal();
            RefreshCartItemsView();
        }

        private void Increment(CartItemModel? item)
        {
            if (item is null) return;

            if (!IsNonInventoryItem(item.ItemType))
            {
                var maxAvailableQuantity = GetMaxAvailableQuantity(item.Stock);
                if (item.Quantity >= maxAvailableQuantity)
                {
                    ShowStockLimitAlert(item.DisplayName, maxAvailableQuantity);
                    return;
                }
                item.Quantity = Math.Min(item.Quantity + 1m, maxAvailableQuantity);
            }
            else
            {
                item.Quantity += 1m;
            }

            var product = _allProducts.FirstOrDefault(p => p.ItemID == item.ItemID && string.Equals(p.Code, item.Code, StringComparison.OrdinalIgnoreCase));
            if (product is not null) product.CartQuantity = item.Quantity;
            RecalculateTotal();
            RefreshCartItemsView();
        }

        private void Decrement(CartItemModel? item)
        {
            if (item is null) return;
            item.Quantity--;
            if (item.Quantity <= 0)
                CartItems.Remove(item);
            if (CartItems.Count == 0)
                ResetExonerationState();
            else
                NormalizeAppliedExonerationState();
            var product = _allProducts.FirstOrDefault(p => p.ItemID == item.ItemID && string.Equals(p.Code, item.Code, StringComparison.OrdinalIgnoreCase));
            if (product is not null) product.CartQuantity = Math.Max(0m, item.Quantity);
            RecalculateTotal();
            RefreshCartItemsView();
        }

        private void DecrementProduct(ProductModel? product)
        {
            if (product is null) return;
            var existing = CartItems.FirstOrDefault(c => c.ItemID == product.ItemID && string.Equals(c.Code, product.Code, StringComparison.OrdinalIgnoreCase));
            if (existing is null) return;
            existing.Quantity--;
            if (existing.Quantity <= 0)
                CartItems.Remove(existing);
            if (CartItems.Count == 0)
                ResetExonerationState();
            else
                NormalizeAppliedExonerationState();
            product.CartQuantity = Math.Max(0m, existing.Quantity);
            RecalculateTotal();
            RefreshCartItemsView();
        }

        private async Task ClearCartAsync()
        {
            if (await TryCancelRecoveredHoldAsync())
                return;

            ClearCart();
        }

        private void ClearCart()
        {
            _editingOrderId = 0;
            _editingHoldId = 0;
            _editingHoldSummary = null;
            ResetExonerationState();
            CartItems.Clear();
            DiscountPercent = 0;
            foreach (var p in _allProducts)
                p.CartQuantity = 0;
            RecalculateTotal();
            RefreshCartItemsView();
        }

        private void RefreshCartItemsView()
        {
            var ordered = ApplyCartSort(CartItems).ToList();

            FilteredCartItems.Clear();
            foreach (var item in ordered)
                FilteredCartItems.Add(item);

            OnPropertyChanged(nameof(CartItemsSummaryText));
            OnPropertyChanged(nameof(CartEmptyText));
            OnPropertyChanged(nameof(CartCountText));
            OnPropertyChanged(nameof(HasSelectedItems));
            OnPropertyChanged(nameof(SelectedCountText));
            ((Command)ApplyBulkDiscountCommand).ChangeCanExecute();
        }

        private void ToggleCartSort(string? field)
        {
            if (string.IsNullOrWhiteSpace(field))
                return;

            if (SelectedCartSortField == field)
            {
                IsCartSortDescending = !IsCartSortDescending;
                return;
            }

            IsCartSortDescending = true;
            SelectedCartSortField = field;
        }

        private void OnCartSortChanged()
        {
            OnPropertyChanged(nameof(CartSortText));
            OnPropertyChanged(nameof(IsCartSortByName));
            OnPropertyChanged(nameof(IsCartSortByCode));
            OnPropertyChanged(nameof(IsCartSortByPrice));
            OnPropertyChanged(nameof(IsCartSortByUnits));
            OnPropertyChanged(nameof(NameHeaderText));
            OnPropertyChanged(nameof(CodeHeaderText));
            OnPropertyChanged(nameof(QuantityHeaderText));
            OnPropertyChanged(nameof(PriceHeaderText));
            RefreshCartItemsView();
        }

        private string GetCartSortHeaderText(string label, bool isActive)
            => isActive ? $"{label} {(IsCartSortDescending ? "↓" : "↑")}" : label;

        private IEnumerable<CartItemModel> ApplyCartSort(IEnumerable<CartItemModel> items)
        {
            return SelectedCartSortField switch
            {
                "Código" => IsCartSortDescending
                    ? items.OrderByDescending(item => item.Code).ThenByDescending(item => item.DisplayName)
                    : items.OrderBy(item => item.Code).ThenBy(item => item.DisplayName),
                "Nombre" => IsCartSortDescending
                    ? items.OrderByDescending(item => NormalizeText(item.DisplayName)).ThenByDescending(item => item.Code)
                    : items.OrderBy(item => NormalizeText(item.DisplayName)).ThenBy(item => item.Code),
                "Precio" => IsCartSortDescending
                    ? items.OrderByDescending(item => item.EffectivePriceColones).ThenByDescending(item => item.DisplayName)
                    : items.OrderBy(item => item.EffectivePriceColones).ThenBy(item => item.DisplayName),
                "Unidades" => IsCartSortDescending
                    ? items.OrderByDescending(item => item.Quantity).ThenByDescending(item => item.DisplayName)
                    : items.OrderBy(item => item.Quantity).ThenBy(item => item.DisplayName),
                _ => items
            };
        }

        private void ClearAllSelections()
        {
            foreach (var item in CartItems)
                item.IsSelected = false;
            RefreshCartItemsView();
        }
    }
}
