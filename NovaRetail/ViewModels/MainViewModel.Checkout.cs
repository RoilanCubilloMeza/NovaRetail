using NovaRetail.Models;
using NovaRetail.Services;
using NovaRetail.State;
using System.Globalization;

namespace NovaRetail.ViewModels
{
    public partial class MainViewModel
    {
        private async Task InvoiceAsync()
        {
            if (CartItems.Count == 0)
            {
                await _dialogService.AlertAsync("Aviso", "El carrito está vacío.", "OK");
                return;
            }

            if (_requireSalesRep && CartItems.Any(c => c.SalesRepID == 0))
            {
                var assign = await _dialogService.ConfirmAsync(
                    "Vendedor requerido",
                    "Hay artículos sin vendedor asignado. ¿Desea asignar uno ahora?",
                    "Asignar", "Cancelar");
                if (assign)
                    await ShowSalesRepPickerForItemsAsync();
                return;
            }

            if (_cachedDiscountCodes.Count == 0)
                await LoadDiscountCodesAsync();
            if (_cachedExonerationCodes.Count == 0)
                await LoadExonerationCodesAsync();

            // Mostrar picker de vendedor antes de abrir el checkout
            await ShowSalesRepPickerBeforeCheckoutAsync();
        }

        private async Task ShowSalesRepPickerBeforeCheckoutAsync()
        {
            // Si ya hay un vendedor activo y todos los artículos tienen vendedor, ir directo al checkout
            if (_activeSalesRep is not null && CartItems.All(c => c.SalesRepID > 0))
            {
                OpenCheckoutPopup();
                return;
            }

            try
            {
                if (_cachedSalesReps.Count == 0)
                {
                    var reps = await _salesRepService.GetAllAsync();
                    _cachedSalesReps.Clear();
                    _cachedSalesReps.AddRange(reps);
                }
            }
            catch { }

            if (_cachedSalesReps.Count == 0)
            {
                // Sin vendedores configurados, ir directo al checkout
                OpenCheckoutPopup();
                return;
            }

            var unassigned = CartItems.Count(c => c.SalesRepID == 0);

            // Si no hay artículos sin vendedor, ir directo al checkout
            if (unassigned == 0)
            {
                OpenCheckoutPopup();
                return;
            }

            var subtitle = $"{unassigned} artículo(s) sin vendedor. Seleccione uno o cancele para continuar.";

            _salesRepPickerContext = SalesRepPickerContext.BeforeCheckout;
            _pendingRepItem = null;
            SalesRepPickerVm.Load(
                _cachedSalesReps,
                canSkip: true,
                title: "Vendedor de la Venta",
                subtitle: subtitle);
            IsSalesRepPickerVisible = true;
        }

        private async void OpenCheckoutPopup()
        {
            CheckoutVm.Load(
                subtotalText: SubtotalText,
                discountAmountText: DiscountAmountText,
                taxText: TaxText,
                totalText: TotalText,
                totalColonesText: TotalColonesText,
                totalColonesValue: _totalColones,
                taxSystemText: TaxSystemText,
                quoteDaysText: QuoteDaysText,
                hasDiscount: DiscountAmount > 0,
                defaultTenderID: _defaultTenderID,
                tenders: Tenders,
                exonerationState: BuildCheckoutExonerationState(),
                salesRep: _activeSalesRep
            );

            // Fetch and display customer credit information
            CustomerCreditInfo? creditInfo = null;
            if (HasClient && CurrentClientHasCredit)
            {
                try
                {
                    var clienteService = GetClienteService();
                    creditInfo = await clienteService.ObtenerCreditoAsync(CurrentClientCreditLookupId);
                }
                catch { /* non-critical */ }
            }
            CheckoutVm.SetCreditInfo(creditInfo);

            IsCheckoutVisible = true;
        }

        private async void OnCheckoutConfirm()
        {
            if (_isProcessingCheckout)
                return;

            var tender = CheckoutVm.SelectedTender;
            if (tender is null)
            {
                await _dialogService.AlertAsync("Facturación", "Seleccione una forma de pago.", "OK");
                return;
            }

            if (CartItems.Any(item => item.ItemID <= 0))
            {
                await _dialogService.AlertAsync("Facturación", "Hay artículos manuales o sin identificador válido en el carrito.", "OK");
                return;
            }

            if (CartItems.Any(item => item.TaxPercentage > 0 && item.TaxID <= 0))
            {
                await _dialogService.AlertAsync("Facturación", "Hay artículos gravados sin TaxID configurado.", "OK");
                return;
            }

            var usesCredit = tender.IsCredit
                || (CheckoutVm.HasSecondTender && CheckoutVm.SecondTender?.IsCredit == true);
            if (usesCredit && !CurrentClientHasCredit)
            {
                var creditMsg = !HasClient
                    ? "Para pagar con crédito debe seleccionar un cliente con cuenta de crédito."
                    : $"{CurrentClientName} no tiene cuenta de crédito habilitada. Cambie la forma de pago.";
                await _dialogService.AlertAsync("Pago con Crédito no permitido", creditMsg, "OK");
                return;
            }

            var currentUser = _userSession.CurrentUser;
            if (currentUser is null)
            {
                await _dialogService.AlertAsync("Facturación", "No hay un usuario autenticado para registrar la venta.", "OK");
                return;
            }

            _isProcessingCheckout = true;
            CheckoutVm.SetCheckoutState(true, "Registrando venta...");
            try
            {
                var request = BuildSaleRequest(currentUser, tender);
                var result = await _saleService.CreateSaleAsync(request);

                if (!result.Ok)
                {
                    var message = string.IsNullOrWhiteSpace(result.Message)
                        ? "No fue posible registrar la venta."
                        : result.Message;
                    CheckoutVm.SetCheckoutState(false, message);
                    await _dialogService.AlertAsync("Facturación", message, "OK");
                    return;
                }

                CheckoutVm.SetCheckoutState(false, string.Empty);
                IsCheckoutVisible = false;

                ReceiptVm.Load(
                    transactionNumber: result.TransactionNumber,
                    clientId: CurrentClientId,
                    clientName: HasClient ? CurrentClientName : _defaultClientName,
                    cashierName: currentUser.DisplayName,
                    registerNumber: _registerIdFromConfig > 0 ? _registerIdFromConfig : 1,
                    storeName: _storeName,
                    storeAddress: _storeAddress,
                    storePhone: _storePhone,
                    cartItems: CartItems.ToList(),
                    subtotalText: SubtotalColonesText,
                    taxText: TaxColonesText,
                    discountText: DiscountColonesText,
                    hasDiscount: DiscountAmount > 0,
                    exonerationText: ExonerationColonesText,
                    hasExoneration: ExonerationAmount > 0,
                    totalText: TotalText,
                    totalColonesText: TotalColonesText,
                    tenderDescription: tender.Description ?? string.Empty,
                    tenderTotalColones: CheckoutVm.HasSecondTender
                        ? Math.Round(
                            CheckoutVm.ChangeColones > 0m
                                ? CheckoutVm.TenderedColones
                                : CheckoutVm.FirstTenderAmount,
                            2)
                        : CheckoutVm.ChangeColones > 0
                            ? Math.Round(_totalColones + CheckoutVm.ChangeColones, 2)
                            : 0m,
                    changeColones: Math.Round(CheckoutVm.ChangeColones, 2),
                    secondTenderDescription: CheckoutVm.HasSecondTender && CheckoutVm.SecondTender != null
                        ? CheckoutVm.SecondTender.Description ?? string.Empty
                        : string.Empty,
                    secondTenderAmountColones: CheckoutVm.HasSecondTender
                        ? Math.Round(CheckoutVm.SecondAmount, 2)
                        : 0m,
                    // Ticket-specific data
                    companyName: _storeName,
                    cedulaJuridica: string.Empty,
                    clave50: !string.IsNullOrWhiteSpace(result.Clave50) ? result.Clave50 : request.CLAVE50,
                    consecutivo: !string.IsNullOrWhiteSpace(result.Clave20) ? result.Clave20 : request.COMPROBANTE_INTERNO,
                    comprobanteTipo: request.COMPROBANTE_TIPO,
                    clientEmail: string.Empty,
                    subtotalColones: _subtotalColones,
                    discountColones: _discountColones,
                    totalColones: _totalColones,
                    taxSystem: _storeTaxSystem
                );
                IsReceiptVisible = true;

                var cartSnapshot = CartItems.ToList();
                _ = SaveInvoiceHistoryAsync(result, request, tender, cartSnapshot);

                await ResetStateAfterCompletedCartAsync();
            }
            catch (Exception ex)
            {
                CheckoutVm.SetCheckoutState(false, ex.Message);
                await _dialogService.AlertAsync("Facturación", ex.Message, "OK");
            }
            finally
            {
                if (IsCheckoutVisible && CheckoutVm.IsSubmitting)
                    CheckoutVm.SetCheckoutState(false);

                _isProcessingCheckout = false;
            }
        }

        private async Task ResetStateAfterCompletedCartAsync()
        {
            ClearCart();
            await ResetCatalogAfterCheckoutAsync();
            _appStore.Dispatch(new SetCurrentClientAction(string.Empty, string.Empty, false));
            CheckoutVm.ExonerationAuthorization = string.Empty;
        }

        private async Task SaveInvoiceHistoryAsync(
            NovaRetailCreateSaleResponse result,
            NovaRetailCreateSaleRequest request,
            TenderModel tender,
            List<CartItemModel> cartSnapshot)
        {
            try
            {
                var currentUser = _userSession.CurrentUser;

                var secondDescription = CheckoutVm.HasSecondTender && CheckoutVm.SecondTender != null
                    ? CheckoutVm.SecondTender.Description ?? string.Empty
                    : string.Empty;
                var secondAmount = CheckoutVm.HasSecondTender
                    ? Math.Round(CheckoutVm.SecondAmount, 2)
                    : 0m;
                var tenderTotalColones = CheckoutVm.HasSecondTender
                    ? Math.Round(CheckoutVm.ChangeColones > 0m ? CheckoutVm.TenderedColones : CheckoutVm.FirstTenderAmount, 2)
                    : CheckoutVm.ChangeColones > 0
                        ? Math.Round(_totalColones + CheckoutVm.ChangeColones, 2)
                        : 0m;

                var entry = new InvoiceHistoryEntry
                {
                    TransactionNumber         = result.TransactionNumber,
                    ComprobanteTipo           = request.COMPROBANTE_TIPO,
                    Clave50                   = !string.IsNullOrWhiteSpace(result.Clave50) ? result.Clave50 : request.CLAVE50,
                    Consecutivo               = !string.IsNullOrWhiteSpace(result.Clave20) ? result.Clave20 : request.COMPROBANTE_INTERNO,
                    ClientId                  = HasClient ? CurrentClientId : _defaultClientId,
                    ClientName                = HasClient ? CurrentClientName : _defaultClientName,
                    CashierName               = currentUser?.DisplayName ?? string.Empty,
                    RegisterNumber            = _registerIdFromConfig > 0 ? _registerIdFromConfig : 1,
                    StoreName                 = _storeName,
                    SubtotalColones           = _subtotalColones,
                    DiscountColones           = _discountColones,
                    ExonerationColones        = _exonerationColones,
                    TaxColones                = _taxColones,
                    TotalColones              = _totalColones,
                    ChangeColones             = Math.Round(CheckoutVm.ChangeColones, 2),
                    TenderDescription         = tender.Description ?? string.Empty,
                    TenderTotalColones        = tenderTotalColones,
                    SecondTenderDescription   = secondDescription,
                    SecondTenderAmountColones = secondAmount,
                    Lines = cartSnapshot.Select(item =>
                    {
                        var grossUnit = item.EffectivePriceColones;
                        var discountFactor = 1m - item.DiscountPercent / 100m;
                        var netUnit = Math.Round(grossUnit * discountFactor, 2);
                        var netLine = Math.Round(grossUnit * item.Quantity * discountFactor, 2);
                        return new InvoiceHistoryLine
                        {
                            DisplayName        = item.DisplayName,
                            Code               = item.Code ?? string.Empty,
                            Quantity           = item.Quantity,
                            TaxPercentage      = item.TaxPercentage,
                            UnitPriceColones   = item.HasDiscount ? netUnit : grossUnit,
                            LineTotalColones   = item.HasDiscount ? netLine : Math.Round(grossUnit * item.Quantity, 2),
                            HasDiscount        = item.HasDiscount,
                            DiscountPercent    = item.DiscountPercent,
                            HasExoneration     = item.HasExoneration,
                            ExonerationPercent = item.ExonerationPercent,
                            HasOverridePrice   = item.HasDownwardPriceOverride
                        };
                    }).ToList()
                };

                await _invoiceHistoryService.AddAsync(entry);
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"[InvoiceHistory] Error al guardar historial: {ex.GetType().Name}: {ex.Message}");
                System.Diagnostics.Debug.WriteLine($"[InvoiceHistory] Path: {System.IO.Path.Combine(FileSystem.AppDataDirectory, "invoice_history.json")}");
            }
        }

        private NovaRetailCreateSaleRequest BuildSaleRequest(LoginUserModel currentUser, TenderModel tender)
        {
            var currencyCode = tender.CurrencyID == 2 ? "USD" : "CRC";
            var medioPagoCodigo = ResolveMedioPagoCodigo(tender);

            var firstAmount = Math.Round(
                CheckoutVm.HasSecondTender && CheckoutVm.FirstTenderAmount > 0
                    ? CheckoutVm.FirstTenderAmount
                    : _totalColones, 2);

            var change = Math.Round(CheckoutVm.ChangeColones, 2);
            var amountForeign = tender.CurrencyID == 2
                ? Math.Round(firstAmount / (_exchangeRate > 0 ? _exchangeRate : 1m), 2)
                : firstAmount;

            var tenders = new List<NovaRetailSaleTenderRequest>
            {
                new()
                {
                    RowNo = 1,
                    TenderID = tender.ID,
                    PaymentID = 0,
                    Description = tender.Description,
                    Amount = firstAmount,
                    AmountForeign = amountForeign,
                    RoundingError = 0m,
                    MedioPagoCodigo = medioPagoCodigo
                }
            };

            if (CheckoutVm.HasSecondTender && CheckoutVm.SecondTender != null && CheckoutVm.SecondAmount > 0m)
            {
                var secondAmount = Math.Round(CheckoutVm.SecondAmount, 2);
                var secondMedioPago = ResolveMedioPagoCodigo(CheckoutVm.SecondTender);
                var secondForeign = CheckoutVm.SecondTender.CurrencyID == 2
                    ? Math.Round(secondAmount / (_exchangeRate > 0 ? _exchangeRate : 1m), 2)
                    : secondAmount;

                tenders.Add(new NovaRetailSaleTenderRequest
                {
                    RowNo = 2,
                    TenderID = CheckoutVm.SecondTender.ID,
                    PaymentID = 0,
                    Description = CheckoutVm.SecondTender.Description,
                    Amount = secondAmount,
                    AmountForeign = secondForeign,
                    RoundingError = 0m,
                    MedioPagoCodigo = secondMedioPago
                });
            }

            var recallId = _editingHoldId > 0
                ? _editingHoldId
                : _editingOrderId > 0
                    ? _editingOrderId
                    : 0;

            var recallType = _editingHoldId > 0
                ? HoldRecallType
                : _editingOrderId > 0
                    ? QuoteRecallType
                    : 0;

            return new NovaRetailCreateSaleRequest
            {
                StoreID = currentUser.StoreId > 0 ? currentUser.StoreId
                        : _storeIdFromConfig > 0 ? _storeIdFromConfig
                        : 1,
                RegisterID = _registerIdFromConfig > 0 ? _registerIdFromConfig : 1,
                CashierID = ParseCashierId(currentUser),
                CustomerID = HasClient ? CurrentClientCustomerId : 0,
                ShipToID = 0,
                Comment = string.Empty,
                ReferenceNumber = string.Empty,
                RecallID = recallId,
                RecallType = recallType,
                TransactionTime = null,
                TotalChange = change,
                AllowNegativeInventory = false,
                CurrencyCode = currencyCode,
                TipoCambio = (_exchangeRate > 0 ? _exchangeRate : 1m).ToString(CultureInfo.InvariantCulture),
                CondicionVenta = (tender.IsCredit || (CheckoutVm.HasSecondTender && CheckoutVm.SecondTender?.IsCredit == true)) ? "02" : "01",
                CodCliente = HasClient ? CurrentClientId : _defaultClientId,
                NombreCliente = HasClient ? CurrentClientName : _defaultClientName,
                CedulaTributaria = HasClient ? CurrentClientId : string.Empty,
                Exonera = (short)(CartItems.Any(item => item.HasExoneration) ? 1 : 0),
                InsertarTiqueteEspera = true,
                COMPROBANTE_TIPO = HasClient && IsCurrentClientReceiver ? "01" : "04",
                COMPROBANTE_SITUACION = "1",
                COD_SUCURSAL = (_storeIdFromConfig > 0 ? _storeIdFromConfig : currentUser.StoreId > 0 ? currentUser.StoreId : 1).ToString("000", CultureInfo.InvariantCulture),
                TERMINAL_POS = (_registerIdFromConfig > 0 ? _registerIdFromConfig : 1).ToString("00000", CultureInfo.InvariantCulture),
                Items = BuildSaleItems(),
                Tenders = tenders
            };
        }

        private static string ResolveMedioPagoCodigo(TenderModel tender)
        {
            // Si la DB tiene el código de medio de pago configurado, usarlo directamente
            if (!string.IsNullOrWhiteSpace(tender.MedioPagoCodigo))
                return tender.MedioPagoCodigo.Trim();

            // Fallback: derivar del nombre de la forma de pago
            var description = (tender.Description ?? string.Empty).Trim().ToUpperInvariant();

            if (description.Contains("EFECTIVO") || description.Contains("CONTADO"))
                return "01";
            if (description.Contains("TARJETA"))
                return "02";
            if (description.Contains("CR\u00C9DITO") || description.Contains("CREDITO"))
                return "99";
            if (description.Contains("TRANSFER") || description.Contains("SINPE"))
                return "04";

            return string.Empty;
        }

        private List<NovaRetailSaleItemRequest> BuildSaleItems()
        {
            var result = new List<NovaRetailSaleItemRequest>(CartItems.Count);

            for (var index = 0; index < CartItems.Count; index++)
            {
                var item = CartItems[index];
                var lineTotals = CalculateLineTotals(item);
                var quantity = item.Quantity <= 0 ? 1m : item.Quantity;

                // UnitPrice = precio neto por unidad SIN impuesto (base después de descuento/override)
                var netLineColones = lineTotals.TotalColones - lineTotals.TaxColones;
                var unitPrice = Math.Round(netLineColones / quantity, 4);

                // FullPrice = precio de catálogo SIN impuesto (antes de override o descuento)
                // Si hay override de precio, el precio original es UnitPriceColones (catálogo)
                var catalogPriceColones = item.HasOverridePrice ? item.UnitPriceColones : item.EffectivePriceColones;
                var rawFullPrice = catalogPriceColones;
                if (IsTaxIncluded && item.TaxPercentage > 0)
                {
                    var divisor = 1m + (item.TaxPercentage / 100m);
                    rawFullPrice = Math.Round(rawFullPrice / divisor, 4);
                }
                var fullPrice = Math.Round(rawFullPrice, 4);

                var lineDiscountAmount = Math.Round(lineTotals.DiscountColones, 2);
                var lineDiscountPercent = fullPrice > 0
                    ? Math.Round((lineDiscountAmount / (fullPrice * quantity)) * 100m, 4)
                    : 0m;

                // DiscountReasonCodeID: usar el ID seleccionado por el usuario, o resolver desde cache
                var discountReasonCodeID = ResolveDiscountReasonCodeID(item);

                // TaxChangeReasonCodeID: indicar exoneración aplicada
                var taxChangeReasonCodeID = ResolveExonerationReasonCodeID(item);

                result.Add(new NovaRetailSaleItemRequest
                {
                    RowNo = index + 1,
                    ItemID = item.ItemID,
                    Quantity = quantity,
                    UnitPrice = unitPrice,
                    FullPrice = fullPrice,
                    Cost = 0m,
                    Commission = 0m,
                    PriceSource = item.IsUpwardPriceOverride ? _priceOverridePriceSource : 1,
                    SalesRepID = item.SalesRepID,
                    Taxable = item.TaxPercentage > 0,
                    TaxID = item.TaxPercentage > 0 ? item.TaxID : null,
                    SalesTax = Math.Round(lineTotals.TaxColones, 2),
                    LineComment = (item.HasDiscount || (item.HasOverridePrice && !item.IsUpwardPriceOverride)) ? item.DiscountReasonCode : string.Empty,
                    DiscountReasonCodeID = discountReasonCodeID,
                    ReturnReasonCodeID = 0,
                    TaxChangeReasonCodeID = taxChangeReasonCodeID,
                    QuantityDiscountID = 0,
                    ItemType = item.ItemType,
                    ComputedQuantity = 0m,
                    IsAddMoney = false,
                    VoucherID = 0,
                    ExtendedDescription = item.DisplayName,
                    PromotionID = null,
                    PromotionName = string.Empty,
                    // LineDiscountAmount/Percent se omiten (= 0) porque UnitPrice ya refleja
                    // el descuento. Enviarlos causaría doble deducción en Transaction.Total
                    // (SP usa: Total = SUM(UnitPrice × Qty) - SUM(LineDiscountAmount)).
                    LineDiscountAmount = 0m,
                    LineDiscountPercent = 0m,
                    ExTipoDoc = item.HasExoneration && _appliedExoneration is not null ? _appliedExoneration.TipoDocumentoCodigo : string.Empty,
                    ExNumeroDoc = item.HasExoneration && _appliedExoneration is not null ? _appliedExoneration.NumeroDocumento : string.Empty,
                    ExInstitucion = item.HasExoneration && _appliedExoneration is not null ? _appliedExoneration.NombreInstitucion : string.Empty,
                    ExFecha = item.HasExoneration && _appliedExoneration is not null ? _appliedExoneration.FechaEmision : null,
                    ExPorcentaje = item.HasExoneration && _appliedExoneration is not null ? _appliedExoneration.PorcentajeExoneracion : 0m,
                    ExMonto = item.HasExoneration ? Math.Round(lineTotals.ExonerationColones, 2) : 0m
                });
            }

            return result;
        }

        private void RefreshCheckoutPopup()
        {
            CheckoutVm.UpdateTotals(SubtotalText, DiscountAmountText, TaxText, TotalText, TotalColonesText, _totalColones, DiscountAmount > 0);
            CheckoutVm.SetExonerationState(BuildCheckoutExonerationState());
        }
    }
}
