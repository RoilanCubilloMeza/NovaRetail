using NovaRetail.Models;
using NovaRetail.State;

namespace NovaRetail.ViewModels
{
    public partial class MainViewModel
    {
        private async Task SaveQuoteAsync()
        {
            if (CartItems.Count == 0)
            {
                await _dialogService.AlertAsync("Cotización", "El carrito está vacío.", "OK");
                return;
            }

            if (CartItems.Any(item => item.ItemID <= 0))
            {
                await _dialogService.AlertAsync("Cotización", "Hay artículos manuales o sin identificador válido en el carrito.", "OK");
                return;
            }

            var currentUser = _userSession.CurrentUser;
            if (currentUser is null)
            {
                await _dialogService.AlertAsync("Cotización", "No hay un usuario autenticado.", "OK");
                return;
            }

            var confirm = await _dialogService.ConfirmAsync("Cotización", "¿Desea guardar el carrito actual como cotización?", "Guardar", "Cancelar");
            if (!confirm)
                return;

            try
            {
                var storeId = currentUser.StoreId > 0 ? currentUser.StoreId
                    : _storeIdFromConfig > 0 ? _storeIdFromConfig
                    : 1;

                var cashierId = ParseCashierId(currentUser);
                var clientRef = HasClient ? BuildOrderReferenceNumber(CurrentClientId, CurrentClientName) : string.Empty;

                var request = new NovaRetailCreateQuoteRequest
                {
                    OrderID = _editingOrderId,
                    StoreID = storeId,
                    CustomerID = HasClient ? CurrentClientCustomerId : 0,
                    ShipToID = 0,
                    Comment = string.Empty,
                    ReferenceNumber = clientRef,
                    SalesRepID = cashierId,
                    Taxable = true,
                    ExpirationOrDueDate = _quoteDays > 0 ? DateTime.Now.AddDays(_quoteDays) : DateTime.Now.AddDays(30),
                    Tax = Math.Round(_taxColones, 4),
                    Total = Math.Round(_totalColones, 4),
                    Items = BuildQuoteItems()
                };

                NovaRetailCreateQuoteResponse result;
                if (_editingOrderId > 0)
                    result = await _quoteService.UpdateQuoteAsync(request);
                else
                    result = await _quoteService.CreateQuoteAsync(request);

                if (!result.Ok)
                {
                    var message = string.IsNullOrWhiteSpace(result.Message)
                        ? "No fue posible guardar la cotización."
                        : result.Message;
                    await _dialogService.AlertAsync("Cotización", message, "OK");
                    return;
                }

                // Si venía de una factura en espera, eliminar la original
                if (_editingHoldId > 0)
                {
                    try { await _quoteService.DeleteHoldAsync(_editingHoldId); }
                    catch { /* no bloquear si falla la limpieza */ }
                }

                var expiration = _quoteDays > 0 ? (DateTime?)DateTime.Now.AddDays(_quoteDays) : null;
                QuoteReceiptVm.Load(
                    orderID: result.OrderID,
                    expirationDate: expiration,
                    clientId: HasClient ? CurrentClientId : string.Empty,
                    clientName: HasClient ? CurrentClientName : string.Empty,
                    cashierName: currentUser.DisplayName ?? string.Empty,
                    registerNumber: _registerIdFromConfig > 0 ? _registerIdFromConfig : 1,
                    storeName: _storeName,
                    storeAddress: _storeAddress,
                    storePhone: _storePhone,
                    cartItems: CartItems.ToList(),
                    subtotalText: SubtotalColonesText,
                    taxText: TaxColonesText,
                    hasDiscount: DiscountAmount > 0,
                    discountText: DiscountColonesText,
                    hasExoneration: ExonerationAmount > 0,
                    exonerationText: ExonerationColonesText,
                    totalText: TotalText,
                    totalColonesText: TotalColonesText);

                await ResetStateAfterCompletedCartAsync();
                IsQuoteReceiptVisible = true;
            }
            catch (Exception ex)
            {
                await _dialogService.AlertAsync("Cotización", ex.Message, "OK");
            }
        }

        private List<NovaRetailQuoteItemRequest> BuildQuoteItems()
        {
            var result = new List<NovaRetailQuoteItemRequest>(CartItems.Count);

            foreach (var item in CartItems)
            {
                var lineTotals = CalculateLineTotals(item);
                var quantity = item.Quantity <= 0 ? 1m : item.Quantity;

                // Price = precio con impuesto incluido (lo que se muestra al cliente)
                var price = Math.Round(item.EffectivePriceColones * (1m - item.DiscountPercent / 100m), 4);

                // FullPrice = precio de catálogo sin descuento
                var fullPrice = Math.Round(item.EffectivePriceColones, 4);

                // Cost = costo del artículo (0 si no se conoce desde el catálogo)
                var cost = 0m;

                var discountReasonCodeID = ResolveDiscountReasonCodeID(item);
                var taxChangeReasonCodeID = ResolveExonerationReasonCodeID(item);

                result.Add(new NovaRetailQuoteItemRequest
                {
                    ItemID = item.ItemID,
                    Cost = cost,
                    FullPrice = fullPrice,
                    PriceSource = item.IsUpwardPriceOverride ? _priceOverridePriceSource : 1,
                    Price = price,
                    QuantityOnOrder = quantity,
                    SalesRepID = item.SalesRepID,
                    Taxable = item.TaxPercentage > 0,
                    DetailID = 0,
                    Description = item.DisplayName.Length > 30 ? item.DisplayName[..30] : item.DisplayName,
                    Comment = (item.HasDiscount || (item.HasOverridePrice && !item.IsUpwardPriceOverride)) ? item.DiscountReasonCode : string.Empty,
                    DiscountReasonCodeID = discountReasonCodeID,
                    ReturnReasonCodeID = 0,
                    TaxChangeReasonCodeID = taxChangeReasonCodeID
                });
            }

            return result;
        }

        private async Task SaveHoldAsync()
        {
            if (CartItems.Count == 0)
            {
                await _dialogService.AlertAsync("Fac. Espera", "El carrito está vacío.", "OK");
                return;
            }

            if (CartItems.Any(item => item.ItemID <= 0))
            {
                await _dialogService.AlertAsync("Fac. Espera", "Hay artículos manuales o sin identificador válido en el carrito.", "OK");
                return;
            }

            var currentUser = _userSession.CurrentUser;
            if (currentUser is null)
            {
                await _dialogService.AlertAsync("Fac. Espera", "No hay un usuario autenticado.", "OK");
                return;
            }

            var comment = await _dialogService.PromptAsync(
                "Factura en Espera",
                "Ingrese un comentario o descripción para identificar esta factura:",
                accept: "Guardar",
                cancel: "Cancelar",
                placeholder: "Ej. Cliente Juan - mesa 3");

            if (comment is null)
                return;

            try
            {
                var storeId = currentUser.StoreId > 0 ? currentUser.StoreId
                    : _storeIdFromConfig > 0 ? _storeIdFromConfig
                    : 1;

                var cashierId = ParseCashierId(currentUser);
                var clientRef = HasClient ? BuildOrderReferenceNumber(CurrentClientId, CurrentClientName) : string.Empty;

                var request = new NovaRetailCreateQuoteRequest
                {
                    OrderID = _editingHoldId,
                    StoreID = storeId,
                    Type = 2,
                    CustomerID = HasClient ? CurrentClientCustomerId : 0,
                    ShipToID = 0,
                    Comment = comment.Trim(),
                    ReferenceNumber = clientRef,
                    SalesRepID = cashierId,
                    Taxable = true,
                    ExpirationOrDueDate = DateTime.Now.AddDays(1),
                    Tax = Math.Round(_taxColones, 4),
                    Total = Math.Round(_totalColones, 4),
                    Items = BuildQuoteItems()
                };

                NovaRetailCreateQuoteResponse result;
                if (_editingHoldId > 0)
                    result = await _quoteService.UpdateHoldAsync(request);
                else
                    result = await _quoteService.SaveHoldAsync(request);

                if (!result.Ok)
                {
                    var message = string.IsNullOrWhiteSpace(result.Message)
                        ? "No fue posible guardar la factura en espera."
                        : result.Message;
                    await _dialogService.AlertAsync("Fac. Espera", message, "OK");
                    return;
                }

                // Si venía de una cotización, eliminar la original
                if (_editingOrderId > 0)
                {
                    try { await _quoteService.DeleteQuoteAsync(_editingOrderId); }
                    catch { /* no bloquear si falla la limpieza */ }
                }

                await _dialogService.AlertAsync("Fac. Espera", $"Factura en espera #{result.OrderID} guardada exitosamente.", "OK");
                await ResetStateAfterCompletedCartAsync();
            }
            catch (Exception ex)
            {
                await _dialogService.AlertAsync("Fac. Espera", ex.Message, "OK");
            }
        }

        public async Task<bool> TryCancelRecoveredHoldAsync()
        {
            if (_editingHoldId <= 0 || CartItems.Count == 0 || _isCancellingRecoveredHold || HasBlockingOverlayVisible())
                return false;

            _isCancellingRecoveredHold = true;
            var holdId = _editingHoldId;

            try
            {
                var confirm = await _dialogService.ConfirmAsync(
                    "Cancelar factura en espera recuperada",
                    BuildRecoveredHoldDeleteMessage(holdId),
                    "Sí",
                    "No");

                if (!confirm)
                    return true;

                var result = await _quoteService.DeleteHoldAsync(holdId);
                if (!result.Ok)
                {
                    var message = string.IsNullOrWhiteSpace(result.Message)
                        ? $"No fue posible eliminar la factura en espera #{holdId}."
                        : result.Message;
                    await _dialogService.AlertAsync("Fac. Espera", message, "OK");
                    return true;
                }

                ClearCart();
                _appStore.Dispatch(new SetCurrentClientAction(string.Empty, string.Empty, false));
                await ResetCatalogAfterCheckoutAsync();
                return true;
            }
            catch (Exception ex)
            {
                await _dialogService.AlertAsync("Fac. Espera", ex.Message, "OK");
                return true;
            }
            finally
            {
                _isCancellingRecoveredHold = false;
            }
        }

        public async Task<bool> TryCancelRecoveredQuoteAsync()
        {
            if (_editingOrderId <= 0 || CartItems.Count == 0 || _isCancellingRecoveredQuote || HasBlockingOverlayVisible())
                return false;

            _isCancellingRecoveredQuote = true;
            var orderId = _editingOrderId;

            try
            {
                var confirm = await _dialogService.ConfirmAsync(
                    "Cancelar cotización recuperada",
                    BuildRecoveredQuoteCancelMessage(orderId),
                    "Sí",
                    "No");

                if (!confirm)
                    return true;

                var result = await _quoteService.DeleteQuoteAsync(orderId);
                if (!result.Ok)
                {
                    var message = string.IsNullOrWhiteSpace(result.Message)
                        ? $"No fue posible cancelar la cotización #{orderId}."
                        : result.Message;
                    await _dialogService.AlertAsync("Cotización", message, "OK");
                    return true;
                }

                ClearCart();
                _appStore.Dispatch(new SetCurrentClientAction(string.Empty, string.Empty, false));
                await ResetCatalogAfterCheckoutAsync();
                return true;
            }
            catch (Exception ex)
            {
                await _dialogService.AlertAsync("Cotización", ex.Message, "OK");
                return true;
            }
            finally
            {
                _isCancellingRecoveredQuote = false;
            }
        }

        private string BuildRecoveredHoldDeleteMessage(int holdId)
        {
            var holdDescription = string.IsNullOrWhiteSpace(_editingHoldSummary?.Comment)
                ? "Sin descripción"
                : _editingHoldSummary.Comment.Trim();

            var holdDate = _editingHoldSummary?.Time;
            var holdDateText = holdDate.HasValue
                ? holdDate.Value.ToString("dd/MM/yyyy HH:mm")
                : "No disponible";

            var lines = new List<string>
            {
                $"Factura en espera: #{holdId}",
                $"Descripción: {holdDescription}",
                $"Fecha: {holdDateText}"
            };

            lines.Add(string.Empty);
            lines.Add("Si continúa:");
            lines.Add("- La factura se eliminará de la lista de facturas en espera.");
            lines.Add("- El carrito actual se vaciará.");
            lines.Add(string.Empty);
            lines.Add("Esta acción no se puede deshacer.");
            lines.Add("¿Desea continuar?");

            return string.Join(Environment.NewLine, lines);
        }

        private string BuildRecoveredQuoteCancelMessage(int orderId)
        {
            var quoteDescription = string.IsNullOrWhiteSpace(_editingQuoteSummary?.Comment)
                ? string.IsNullOrWhiteSpace(_editingQuoteSummary?.DisplayClient)
                    ? "Sin descripción"
                    : _editingQuoteSummary.DisplayClient
                : _editingQuoteSummary.Comment.Trim();

            var quoteDate = _editingQuoteSummary?.Time;
            var quoteDateText = quoteDate.HasValue
                ? quoteDate.Value.ToString("dd/MM/yyyy HH:mm")
                : "No disponible";

            var lines = new List<string>
            {
                $"Cotización: #{orderId}",
                $"Detalle: {quoteDescription}",
                $"Fecha: {quoteDateText}"
            };

            lines.Add(string.Empty);
            lines.Add("Si continúa:");
            lines.Add("- La cotización se marcará como cerrada.");
            lines.Add("- Dejará de verse en la ventana de cotizaciones.");
            lines.Add("- El carrito actual se vaciará.");
            lines.Add(string.Empty);
            lines.Add("El registro no se elimina de la base de datos.");
            lines.Add("¿Desea continuar?");

            return string.Join(Environment.NewLine, lines);
        }

        private async Task OnOrderCancelRequestedAsync(NovaRetailOrderSummary order)
        {
            if (order is null || !order.CanCancel)
                return;

            var confirm = await _dialogService.ConfirmAsync(
                "Cancelar cotización",
                BuildOrderSearchQuoteCancelMessage(order),
                "Sí",
                "No");

            if (!confirm)
                return;

            try
            {
                var result = await _quoteService.DeleteQuoteAsync(order.OrderID);
                if (!result.Ok)
                {
                    var message = string.IsNullOrWhiteSpace(result.Message)
                        ? $"No fue posible cancelar la cotización #{order.OrderID}."
                        : result.Message;
                    await _dialogService.AlertAsync("Cotización", message, "OK");
                    return;
                }

                if (_editingOrderId == order.OrderID)
                {
                    _editingOrderId = 0;
                    _editingQuoteSummary = null;
                }

                await SearchOrdersAsync(OrderSearchVm.SearchText);
                await _dialogService.AlertAsync("Cotización", $"Cotización #{order.OrderID} cancelada exitosamente.", "OK");
            }
            catch (Exception ex)
            {
                await _dialogService.AlertAsync("Cotización", ex.Message, "OK");
            }
        }

        private string BuildOrderSearchQuoteCancelMessage(NovaRetailOrderSummary order)
        {
            var quoteDescription = string.IsNullOrWhiteSpace(order.Comment)
                ? string.IsNullOrWhiteSpace(order.DisplayClient)
                    ? "Sin descripción"
                    : order.DisplayClient
                : order.Comment.Trim();

            var lines = new List<string>
            {
                $"Cotización: #{order.OrderID}",
                $"Detalle: {quoteDescription}",
                $"Fecha: {order.DisplayDate}"
            };

            lines.Add(string.Empty);
            lines.Add("Si continúa:");
            lines.Add("- La cotización se marcará como cerrada.");
            lines.Add("- Dejará de mostrarse en la ventana de cotizaciones.");
            lines.Add(string.Empty);
            lines.Add("El registro no se elimina de la base de datos.");
            lines.Add("¿Desea continuar?");

            return string.Join(Environment.NewLine, lines);
        }

        private void ResetRecoveredOrderTracking()
        {
            _editingOrderId = 0;
            _editingHoldId = 0;
            _editingHoldSummary = null;
            _editingQuoteSummary = null;
        }

        private bool HasBlockingOverlayVisible()
            => IsItemActionVisible
                || IsDiscountPopupVisible
                || IsPriceJustVisible
                || IsCheckoutVisible
                || IsReceiptVisible
                || IsManualExonerationVisible
                || IsOrderSearchVisible
                || IsQuoteReceiptVisible
                || IsSalesRepPickerVisible
                || IsCustomerSearchVisible
                || IsCreditPaymentSearchVisible
                || IsCreditPaymentDetailVisible;

        private async Task OpenOrderSearchAsync(int orderType, string title)
        {
            OrderSearchVm.Load(orderType, title);
            IsOrderSearchVisible = true;

            await SearchOrdersAsync(string.Empty);
        }

        private async Task SearchOrdersAsync(string search)
        {
            if (OrderSearchVm.IsBusy)
                return;

            OrderSearchVm.SetBusy(true);
            await Task.Yield();
            try
            {
                var storeId = _storeIdFromConfig > 0 ? _storeIdFromConfig : 0;
                NovaRetailListOrdersResponse result;

                if (OrderSearchVm.OrderType == 2)
                    result = await _quoteService.ListHoldsAsync(storeId, search);
                else
                    result = await _quoteService.ListOrdersAsync(storeId, OrderSearchVm.OrderType, search);

                if (!result.Ok)
                {
                    OrderSearchVm.SetError(result.Message);
                    return;
                }

                OrderSearchVm.SetOrders(result.Orders);
            }
            catch (Exception ex)
            {
                OrderSearchVm.SetError(ex.Message);
            }
            finally
            {
                OrderSearchVm.SetBusy(false);
            }
        }

        private async void OnOrderSelectedAsync(NovaRetailOrderSummary order)
        {
            if (order is null)
                return;

            OrderSearchVm.SetBusy(true);
            try
            {
                NovaRetailOrderDetailResponse detail;
                if (order.Type == 2)
                    detail = await _quoteService.GetHoldDetailAsync(order.OrderID);
                else
                    detail = await _quoteService.GetOrderDetailAsync(order.OrderID);

                if (!detail.Ok || detail.Order is null)
                {
                    OrderSearchVm.SetError(detail.Message);
                    return;
                }

                IsOrderSearchVisible = false;

                if (CartItems.Count > 0)
                {
                    var replace = await _dialogService.ConfirmAsync(
                        "Recuperar Orden",
                        "El carrito tiene artículos. ¿Desea reemplazarlos con los de esta orden?",
                        "Reemplazar", "Cancelar");
                    if (!replace)
                        return;

                    ClearCart();
                }

                await EnsureSalesRepsLoadedAsync();

                foreach (var entry in detail.Order.Entries)
                {
                    var recoveredSalesRep = FindCachedSalesRep(entry.SalesRepID);
                    var recoveredSalesRepName = recoveredSalesRep?.Nombre ?? (entry.SalesRepID > 0 ? $"Vendedor #{entry.SalesRepID}" : string.Empty);

                    var cartItem = new CartItemModel
                    {
                        ItemID = entry.ItemID,
                        Emoji = "📋",
                        Name = entry.Description,
                        Code = entry.ItemID.ToString(),
                        UnitPriceColones = entry.Price,
                        UnitPrice = ConvertFromColones(entry.Price),
                        TaxPercentage = entry.Taxable ? _defaultTaxPercentage : 0m,
                        TaxID = entry.TaxID,
                        Cabys = string.Empty,
                        ItemType = entry.ItemType,
                        Stock = entry.QuantityOnOrder > 0 ? entry.QuantityOnOrder : 1m,
                        Quantity = entry.QuantityOnOrder > 0 ? entry.QuantityOnOrder : 1m,
                        SalesRepID = entry.SalesRepID,
                        SalesRepName = recoveredSalesRepName
                    };
                    CartItems.Add(cartItem);
                }

                SyncRecoveredSalesRep(detail.Order.Entries);

                RecalculateTotal();
                RefreshCartItemsView();

                ResetRecoveredOrderTracking();

                if (order.Type == 2)
                {
                    _editingHoldId = order.OrderID;
                    _editingHoldSummary = order;
                    _editingQuoteSummary = null;
                }
                else
                {
                    _editingOrderId = order.OrderID;
                    _editingQuoteSummary = order;
                    _editingHoldSummary = null;
                }

                // Restaurar datos del cliente desde la cotización
                var savedClientId = order.ParseClientId();
                var savedClientName = order.ParseClientName();
                if (!string.IsNullOrWhiteSpace(savedClientId))
                    SetCliente(savedClientId, savedClientName, accountNumber: savedClientId, customerId: order.CustomerID);

                var typeName = order.Type == 2 ? "Factura en espera" : "Cotización";
                await _dialogService.AlertAsync("Orden recuperada",
                    $"{typeName} #{order.OrderID} cargada al carrito con {detail.Order.Entries.Count} artículo(s).", "OK");
            }
            catch (Exception ex)
            {
                OrderSearchVm.SetError(ex.Message);
            }
            finally
            {
                OrderSearchVm.SetBusy(false);
            }
        }

        private void SyncRecoveredSalesRep(IEnumerable<NovaRetailOrderEntry> entries)
        {
            var recoveredSalesRepIds = entries
                .Where(entry => entry.SalesRepID > 0)
                .Select(entry => entry.SalesRepID)
                .Distinct()
                .ToList();

            if (recoveredSalesRepIds.Count != 1)
            {
                SetActiveSalesRep(null);
                return;
            }

            var salesRepId = recoveredSalesRepIds[0];
            var recoveredSalesRep = FindCachedSalesRep(salesRepId)
                ?? new SalesRepModel
                {
                    ID = salesRepId,
                    Number = salesRepId.ToString(),
                    Nombre = $"Vendedor #{salesRepId}"
                };

            SetActiveSalesRep(recoveredSalesRep);
        }
    }
}
