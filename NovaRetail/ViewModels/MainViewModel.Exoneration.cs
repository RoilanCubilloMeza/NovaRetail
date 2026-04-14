using NovaRetail.Models;

namespace NovaRetail.ViewModels
{
    public partial class MainViewModel
    {
        private CheckoutExonerationState BuildCheckoutExonerationState()
        {
            var appliedItemCount = CartItems.Count(c => c.HasExoneration);
            if (_appliedExoneration is null || appliedItemCount == 0)
            {
                return new CheckoutExonerationState
                {
                    HasExoneration = false,
                    Authorization = CheckoutVm.ExonerationAuthorization,
                    SummaryText = "Sin exoneración aplicada.",
                    StatusText = "Ingrese una autorización de Hacienda para validar la exoneración.",
                    ScopeText = CartItems.Any(c => c.IsSelected)
                        ? $"Se aplicará a {CartItems.Count(c => c.IsSelected)} artículo(s) seleccionados."
                        : "Se aplicará a todo el carrito si no hay selección activa."
                };
            }

            var vencimiento = _appliedExoneration.FechaVencimiento?.ToString("dd/MM/yyyy") ?? "—";
            return new CheckoutExonerationState
            {
                HasExoneration = true,
                Authorization = _appliedExonerationAuthorization,
                SummaryText = $"{_appliedExoneration.NombreInstitucion} · {_appliedExoneration.PorcentajeExoneracion:0.##}% · {_appliedExoneration.TipoDocumentoDescripcion}",
                StatusText = $"Doc. {_appliedExoneration.NumeroDocumento} · vence {vencimiento} · {appliedItemCount} artículo(s).",
                ScopeText = _appliedExonerationScopeText
            };
        }

        private async Task ApplyExonerationAsync()
        {
            CheckoutVm.SetBusy(true);

            try
            {
                var authorization = CheckoutVm.ExonerationAuthorization?.Trim() ?? string.Empty;
                var document = await ValidateExonerationDocumentAsync(authorization);
                if (document is null)
                    return;

                var targetItems = GetExonerationTargetItems();
                if (targetItems.Count == 0)
                {
                    await _dialogService.AlertAsync("Exoneración", "No hay artículos disponibles para exonerar.", "OK");
                    return;
                }

                var invalidItems = GetInvalidCabysItems(targetItems, document);
                var eligibleItems = targetItems
                    .Where(item => IsCabysAllowed(item, document))
                    .ToList();

                if (eligibleItems.Count == 0)
                {
                    var detail = string.Join(Environment.NewLine, invalidItems.Take(5));
                    var suffix = invalidItems.Count > 5 ? $"{Environment.NewLine}... y {invalidItems.Count - 5} más." : string.Empty;
                    await _dialogService.AlertAsync("Exoneración", $"Hay artículos sin CABYS válido para esta autorización:{Environment.NewLine}{detail}{suffix}", "OK");
                    return;
                }

                ResetExonerationState();
                var exonReasonCodeID = _cachedExonerationCodes.FirstOrDefault()?.ID ?? 0;
                foreach (var item in eligibleItems)
                {
                    item.ExonerationPercent = document.PorcentajeExoneracion;
                    item.ExonerationReasonCodeID = exonReasonCodeID;
                }

                var scopeText = eligibleItems.Count == CartItems.Count
                    ? "Exoneración aplicada a todo el carrito."
                    : eligibleItems.Count == targetItems.Count
                        ? $"Exoneración aplicada a {eligibleItems.Count} artículo(s) seleccionados."
                        : $"Exoneración aplicada a {eligibleItems.Count} de {targetItems.Count} artículo(s).";

                SetAppliedExoneration(document, authorization, scopeText);

                RecalculateTotal();
                RefreshCartItemsView();

                if (invalidItems.Count > 0)
                {
                    var detail = string.Join(Environment.NewLine, invalidItems.Take(5));
                    var suffix = invalidItems.Count > 5 ? $"{Environment.NewLine}... y {invalidItems.Count - 5} más." : string.Empty;
                    await _dialogService.AlertAsync(
                        "Exoneración",
                        $"Se aplicó la exoneración a los artículos válidos.{Environment.NewLine}No aplicó para:{Environment.NewLine}{detail}{suffix}",
                        "OK");
                }
            }
            finally
            {
                CheckoutVm.SetBusy(false);
            }
        }

        private void ClearExoneration()
        {
            ResetExonerationState();
            RecalculateTotal();
            RefreshCartItemsView();
        }

        private async Task ApplyManualExonerationAsync()
        {
            if (!HasClient)
            {
                await _dialogService.AlertAsync("Exoneración Manual", "Seleccione un cliente antes de aplicar la exoneración.", "OK");
                return;
            }

            var targetItems = GetExonerationTargetItems();
            if (targetItems.Count == 0)
            {
                await _dialogService.AlertAsync("Exoneración Manual", "No hay artículos disponibles para exonerar.", "OK");
                return;
            }

            if (_cachedExonerationCodes.Count == 0)
                await LoadExonerationCodesAsync();

            await LoadExonerationDocumentTypesAsync();

            ManualExonerationVm.Load(CheckoutVm.ExonerationAuthorization, _subtotalColones, CurrentClientName);
            IsManualExonerationVisible = true;
        }

        private async Task OnManualExonerationBuscarAsync(string authorizationNumber)
        {
            ManualExonerationVm.SetBusy(true);
            var result = await _exonerationService.ValidateAsync(authorizationNumber);
            ManualExonerationVm.ApplyApiResult(result);
        }

        private void OnManualExonerationApply(ManualExonerationResult result)
        {
            IsManualExonerationVisible = false;

            var targetItems = GetExonerationTargetItems();
            if (targetItems.Count == 0)
                return;

            ResetExonerationState();
            var exonReasonCodeID = _cachedExonerationCodes.FirstOrDefault()?.ID ?? 0;
            foreach (var item in targetItems)
            {
                item.ExonerationPercent = result.Document.PorcentajeExoneracion;
                item.ExonerationReasonCodeID = exonReasonCodeID;
            }

            var scopeText = targetItems.Count == CartItems.Count
                ? "Exoneración manual aplicada a todo el carrito."
                : $"Exoneración manual aplicada a {targetItems.Count} artículo(s).";

            SetAppliedExoneration(result.Document, result.Authorization, scopeText);
            RecalculateTotal();
            RefreshCartItemsView();
        }

        private async Task ApplyItemExonerationAsync(CartItemModel? item)
        {
            if (item is null)
                return;

            var authorization = await _dialogService.PromptAsync(
                "Exoneración",
                $"Ingrese la autorización de Hacienda para {item.DisplayName}.",
                accept: "Aplicar",
                cancel: "Cancelar",
                placeholder: "Ej. AL-00020402-24",
                initialValue: CheckoutVm.ExonerationAuthorization);

            authorization = authorization?.Trim() ?? string.Empty;
            if (string.IsNullOrWhiteSpace(authorization))
                return;

            var document = await ValidateExonerationDocumentAsync(authorization);
            if (document is null)
                return;

            var invalidItems = GetInvalidCabysItems(new[] { item }, document);
            if (invalidItems.Count > 0)
            {
                await _dialogService.AlertAsync("Exoneración", $"El artículo {item.DisplayName} no tiene un CABYS válido para esta autorización.", "OK");
                return;
            }

            item.ExonerationPercent = document.PorcentajeExoneracion;
            item.ExonerationReasonCodeID = _cachedExonerationCodes.FirstOrDefault()?.ID ?? 0;
            SetAppliedExoneration(document, authorization, $"Exoneración aplicada a {item.DisplayName}.");
            UpdateExonerationEligibility(document);
            RecalculateTotal();
            RefreshCartItemsView();
        }

        private void ClearItemExoneration(CartItemModel? item)
        {
            if (item is null)
                return;

            item.ExonerationPercent = 0m;
            item.ExonerationReasonCodeID = 0;
            NormalizeAppliedExonerationState();
            UpdateExonerationEligibility(_appliedExoneration);
            RecalculateTotal();
            RefreshCartItemsView();
        }

        private void ResetExonerationState()
        {
            foreach (var item in CartItems)
            {
                item.ExonerationPercent = 0m;
                item.ExonerationReasonCodeID = 0;
                item.HasExonerationEligibility = false;
                item.IsExonerationEligible = false;
            }

            _appliedExoneration = null;
            _appliedExonerationAuthorization = string.Empty;
            _appliedExonerationScopeText = string.Empty;
            _appliedExonerationItemCount = 0;
        }

        private async Task<ExonerationModel?> ValidateExonerationDocumentAsync(string authorization)
        {
            if (!HasClient)
            {
                await _dialogService.AlertAsync("Exoneración", "Seleccione un cliente antes de validar la exoneración.", "OK");
                return null;
            }

            if (string.IsNullOrWhiteSpace(authorization))
            {
                await _dialogService.AlertAsync("Exoneración", "Ingrese el número de autorización de Hacienda.", "OK");
                return null;
            }

            var validation = await _exonerationService.ValidateAsync(authorization);
            if (!validation.IsValid || validation.Document is null)
            {
                await _dialogService.AlertAsync("Exoneración", validation.Message, "OK");
                return null;
            }

            var document = validation.Document;
            if (document.IsExpired)
            {
                await _dialogService.AlertAsync("Exoneración", "La autorización de Hacienda ya está vencida.", "OK");
                return null;
            }

            if (!string.IsNullOrWhiteSpace(document.Identificacion) &&
                !string.Equals(NormalizeIdentity(document.Identificacion), NormalizeIdentity(CurrentClientId), StringComparison.OrdinalIgnoreCase))
            {
                await _dialogService.AlertAsync(
                    "Exoneración",
                    $"La identificación de la exoneración no coincide con el cliente seleccionado.{Environment.NewLine}Hacienda: {document.Identificacion}{Environment.NewLine}Cliente actual: {CurrentClientId}",
                    "OK");
                return null;
            }

            return document;
        }

        private void SetAppliedExoneration(ExonerationModel document, string authorization, string scopeText)
        {
            _appliedExoneration = document;
            _appliedExonerationAuthorization = authorization;
            _appliedExonerationScopeText = scopeText;
            _appliedExonerationItemCount = CartItems.Count(c => c.HasExoneration);
            CheckoutVm.ExonerationAuthorization = authorization;
            UpdateExonerationEligibility(document);
        }

        private void NormalizeAppliedExonerationState()
        {
            _appliedExonerationItemCount = CartItems.Count(c => c.HasExoneration);
            if (_appliedExonerationItemCount > 0)
            {
                _appliedExonerationScopeText = _appliedExonerationItemCount == CartItems.Count
                    ? "Exoneración aplicada a todo el carrito."
                    : $"Exoneración aplicada a {_appliedExonerationItemCount} artículo(s).";
                UpdateExonerationEligibility(_appliedExoneration);
                return;
            }

            _appliedExoneration = null;
            _appliedExonerationAuthorization = string.Empty;
            _appliedExonerationScopeText = string.Empty;
            UpdateExonerationEligibility(null);
        }

        private void UpdateExonerationEligibility(ExonerationModel? document)
        {
            foreach (var item in CartItems)
                UpdateExonerationEligibility(item, document);
        }

        private void UpdateExonerationEligibility(CartItemModel item, ExonerationModel? document)
        {
            if (document is null)
            {
                item.HasExonerationEligibility = false;
                item.IsExonerationEligible = false;
                return;
            }

            item.HasExonerationEligibility = true;
            item.IsExonerationEligible = IsCabysAllowed(item, document);
        }

        private static bool IsCabysAllowed(CartItemModel item, ExonerationModel document)
        {
            if (!document.PoseeCabys || document.Cabys.Count == 0)
                return true;

            var cabys = NormalizeCabys(item.Cabys);
            if (string.IsNullOrWhiteSpace(cabys))
                return false;

            return document.Cabys
                .Select(NormalizeCabys)
                .Any(c => string.Equals(c, cabys, StringComparison.OrdinalIgnoreCase));
        }

        private List<CartItemModel> GetExonerationTargetItems()
        {
            var selected = CartItems.Where(c => c.IsSelected).ToList();
            return selected.Count > 0 ? selected : CartItems.ToList();
        }

        private List<string> GetInvalidCabysItems(IEnumerable<CartItemModel> items, ExonerationModel document)
        {
            if (!document.PoseeCabys || document.Cabys.Count == 0)
                return new List<string>();

            var allowedCabys = document.Cabys
                .Select(NormalizeCabys)
                .Where(c => !string.IsNullOrWhiteSpace(c))
                .ToHashSet(StringComparer.OrdinalIgnoreCase);

            return items
                .Where(item => string.IsNullOrWhiteSpace(NormalizeCabys(item.Cabys)) || !allowedCabys.Contains(NormalizeCabys(item.Cabys)))
                .Select(item => $"{item.Code} - {item.DisplayName}")
                .Distinct(StringComparer.OrdinalIgnoreCase)
                .ToList();
        }

        private async Task LoadExonerationCodesAsync()
        {
            try
            {
                var codes = await _productService.GetReasonCodesAsync(6);
                if (codes.Count > 0)
                {
                    _cachedExonerationCodes.Clear();
                    _cachedExonerationCodes.AddRange(codes);
                }
            }
            catch
            {
            }
        }

        private async Task LoadExonerationDocumentTypesAsync()
        {
            try
            {
                var codes = await _productService.GetExonerationDocumentTypesAsync();
                ManualExonerationVm.LoadDocumentTypes(
                    codes.Select(c => new ExonerationDocumentType
                    {
                        Codigo = c.Code,
                        Descripcion = c.Description
                    }));
            }
            catch
            {
            }
        }

        private int ResolveExonerationReasonCodeID(CartItemModel item)
        {
            if (!item.HasExoneration)
                return 0;

            if (item.ExonerationReasonCodeID > 0)
                return item.ExonerationReasonCodeID;

            return _cachedExonerationCodes.FirstOrDefault()?.ID ?? 0;
        }
    }
}
