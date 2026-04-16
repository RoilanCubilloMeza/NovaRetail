using NovaRetail.Data;
using NovaRetail.Models;
using NovaRetail.Services;
using System.Linq;

namespace NovaRetail.ViewModels
{
    public partial class MainViewModel
    {
        // ── Búsqueda de clientes ──

        private bool AreClientShortcutsBlocked()
            => IsItemActionVisible
               || IsPriceJustVisible
               || IsDiscountPopupVisible
               || IsCheckoutVisible
               || IsReceiptVisible
               || IsManualExonerationVisible
               || IsOrderSearchVisible
               || IsQuoteReceiptVisible
               || IsSalesRepPickerVisible
               || IsCustomerSearchVisible
               || IsCreditPaymentSearchVisible
               || IsCreditPaymentDetailVisible;

        public async Task<bool> TryOpenClienteShortcutAsync()
        {
            if (AreClientShortcutsBlocked() || Shell.Current is null)
                return false;

            await Shell.Current.GoToAsync("ClientePage");
            return true;
        }

        public async Task<bool> TryOpenCustomerSearchShortcutAsync()
        {
            if (AreClientShortcutsBlocked())
                return false;

            await OpenCustomerSearchAsync();
            return true;
        }

        private async Task OpenCustomerSearchAsync()
        {
            CustomerSearchVm.Reset();
            IsCustomerSearchVisible = true;
            await SearchCustomersAsync(null);
        }

        private async Task SearchCustomersAsync(string? criteria)
        {
            try
            {
                CustomerSearchVm.SetBusy(true);
                var clienteService = GetClienteService();
                var results = await clienteService.BuscarClientesAsync(criteria);
                CustomerSearchVm.SetCustomers(results);
            }
            catch (Exception ex)
            {
                CustomerSearchVm.SetError($"Error al buscar clientes: {ex.Message}");
            }
            finally
            {
                CustomerSearchVm.SetBusy(false);
            }
        }

        private async void OnCustomerSelected(Models.CustomerLookupModel customer)
        {
            if (customer is null)
                return;

            CustomerLookupModel resolvedCustomer = customer;

            try
            {
                CustomerSearchVm.SetBusy(true);
                resolvedCustomer = await ResolveSelectedCustomerAsync(customer);
            }
            catch
            {
                resolvedCustomer = customer;
            }
            finally
            {
                CustomerSearchVm.SetBusy(false);
            }

            IsCustomerSearchVisible = false;
            var customerType = resolvedCustomer.AccountTypeID switch
            {
                2 => "Cr\u00e9dito",
                3 => "Gobierno",
                4 => "Exportaci\u00f3n",
                _ => "Contado"
            };
            var isReceiver = !string.IsNullOrWhiteSpace(resolvedCustomer.Email);
            SetCliente(
                resolvedCustomer.ResolvedClientId,
                resolvedCustomer.FullName,
                isReceiver: isReceiver,
                customerType: customerType,
                accountNumber: resolvedCustomer.AccountNumber,
                customerId: resolvedCustomer.CustomerId);
        }

        private async Task<CustomerLookupModel> ResolveSelectedCustomerAsync(CustomerLookupModel customer)
        {
            if (customer.CustomerId > 0 && !string.IsNullOrWhiteSpace(customer.TaxNumber))
                return customer;

            var lookup = !string.IsNullOrWhiteSpace(customer.AccountNumber)
                ? customer.AccountNumber
                : customer.ResolvedClientId;

            if (string.IsNullOrWhiteSpace(lookup))
                return customer;

            var clienteService = GetClienteService();
            var refreshed = await clienteService.BuscarClientesAsync(lookup);

            return refreshed.FirstOrDefault(c =>
                       !string.IsNullOrWhiteSpace(customer.AccountNumber) &&
                       string.Equals(c.AccountNumber?.Trim(), customer.AccountNumber.Trim(), StringComparison.OrdinalIgnoreCase))
                   ?? refreshed.FirstOrDefault(c =>
                       !string.IsNullOrWhiteSpace(customer.ResolvedClientId) &&
                       string.Equals(c.ResolvedClientId?.Trim(), customer.ResolvedClientId.Trim(), StringComparison.OrdinalIgnoreCase))
                   ?? customer;
        }

        private IClienteService GetClienteService()
        {
            // Resolve from the service provider via the Shell
            return Shell.Current.Handler?.MauiContext?.Services.GetService<IClienteService>()
                ?? throw new InvalidOperationException("IClienteService not available.");
        }

        // ── Abonos a crédito ──

        private async Task OpenCreditPaymentSearchAsync()
        {
            CreditPaymentSearchVm.Reset();
            IsCreditPaymentSearchVisible = true;
            await SearchCreditCustomersAsync(null);
        }

        private async Task SearchCreditCustomersAsync(string? criteria)
        {
            try
            {
                CreditPaymentSearchVm.SetBusy(true);
                CreditPaymentSearchVm.SetCustomers([], "Buscando clientes con crédito...");
                var clienteService = GetClienteService();
                var results = await clienteService.BuscarClientesCreditoAsync(criteria);

                if (results.Count == 0 && !string.IsNullOrWhiteSpace(criteria))
                    CreditPaymentSearchVm.SetCustomers(results, $"No se encontraron clientes para \"{criteria}\".");
                else
                    CreditPaymentSearchVm.SetCustomers(results);
            }
            catch (Exception ex)
            {
                CreditPaymentSearchVm.SetError($"Error al buscar clientes con crédito: {ex.Message}");
            }
            finally
            {
                CreditPaymentSearchVm.SetBusy(false);
            }
        }

        private async void OnCreditCustomerSelected(Models.CustomerCreditInfo customer)
        {
            try
            {
                IsCreditPaymentSearchVisible = false;
                CreditPaymentDetailVm.LoadCustomer(customer);
                IsCreditPaymentDetailVisible = true;

                // Load tenders and open entries in parallel
                var tendersTask = LoadCreditTendersAsync();
                var entriesTask = LoadCreditEntriesAsync(customer.AccountNumber);
                await Task.WhenAll(tendersTask, entriesTask);
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"[CreditSelect] FATAL ERROR: {ex}");
                IsCreditPaymentDetailVisible = true;
            }
        }

        private async Task LoadCreditTendersAsync()
        {
            try
            {
                var allTenders = await _storeConfigService.GetTendersAsync();
                var settings = await _parametrosService.GetTenderSettingsAsync();
                if (settings is not null && !string.IsNullOrWhiteSpace(settings.PaymentsTenderCods))
                {
                    var allowed = new HashSet<int>();
                    foreach (var code in settings.PaymentsTenderCods.Split(new[] { ',', '_' }, StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries))
                    {
                        if (int.TryParse(code, out var id))
                            allowed.Add(id);
                    }
                    if (allowed.Count > 0)
                        allTenders = allTenders.Where(t => allowed.Contains(t.ID)).ToList();
                }
                await MainThread.InvokeOnMainThreadAsync(() => CreditPaymentDetailVm.LoadTenders(allTenders));
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"[CreditSelect] Tender load error: {ex.Message}");
            }
        }

        private async Task LoadCreditEntriesAsync(string accountNumber)
        {
            try
            {
                var clienteService = GetClienteService();
                var entries = await clienteService.ObtenerCuentasAbiertasAsync(accountNumber);
                await MainThread.InvokeOnMainThreadAsync(() => CreditPaymentDetailVm.LoadOpenEntries(entries));
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"[CreditSelect] Entries load error: {ex.Message}");
                await MainThread.InvokeOnMainThreadAsync(() =>
                    CreditPaymentDetailVm.SetError($"Error al cargar cuentas abiertas: {ex.Message}"));
            }
        }

        private async Task ProcessAbonoAsync(Models.AbonoPaymentRequest request)
        {
            try
            {
                CreditPaymentDetailVm.SetBusy(true);
                var clienteService = GetClienteService();
                var currentUser = _userSession.CurrentUser;
                var cashierId = currentUser is not null ? ParseCashierId(currentUser) : 1;

                request.CashierId = cashierId;
                request.StoreId = _storeIdFromConfig;

                var (success, message) = await clienteService.RegistrarAbonoAsync(request);

                if (success)
                {
                    await MainThread.InvokeOnMainThreadAsync(() => CreditPaymentDetailVm.SetSuccess());
                    // Refresh credit info + open entries in parallel
                    var creditTask = clienteService.ObtenerCreditoAsync(request.AccountNumber);
                    var entriesTask = clienteService.ObtenerCuentasAbiertasAsync(request.AccountNumber);
                    await Task.WhenAll(creditTask, entriesTask);

                    var updated = await creditTask;
                    var refreshedEntries = await entriesTask;

                    await MainThread.InvokeOnMainThreadAsync(() =>
                    {
                        if (updated is not null)
                            CreditPaymentDetailVm.RefreshCredit(updated);

                        CreditPaymentDetailVm.LoadOpenEntries(refreshedEntries);
                    });
                }
                else
                {
                    await MainThread.InvokeOnMainThreadAsync(() =>
                        CreditPaymentDetailVm.SetError(message ?? "No se pudo registrar el abono. Intente de nuevo."));
                }
            }
            catch (Exception ex)
            {
                await MainThread.InvokeOnMainThreadAsync(() =>
                    CreditPaymentDetailVm.SetError($"Error: {ex.Message}"));
            }
            finally
            {
                await MainThread.InvokeOnMainThreadAsync(() => CreditPaymentDetailVm.SetBusy(false));
            }
        }
    }
}
