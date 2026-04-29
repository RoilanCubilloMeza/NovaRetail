using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Runtime.CompilerServices;
using System.Windows.Input;
using Microsoft.Maui.ApplicationModel;
using NovaRetail.Data;
using NovaRetail.Messages;
using NovaRetail.Models;
using NovaRetail.Services;

namespace NovaRetail.ViewModels;

public class ParametrosViewModel : INotifyPropertyChanged
{
    private readonly IParametrosService _service;
    private readonly IStoreConfigService _storeConfig;
    private readonly IDialogService _dialog;

    private bool _isBusy;
    private bool _isSaving;
    private string _statusMessage = string.Empty;
    private string _activeSection = "Parametros";

    // Parámetros generales
    public ObservableCollection<ParametroEditItem> Parametros { get; } = new();

    // Selección visual de tenders
    public ObservableCollection<TenderCheckItem> TenderOptions { get; } = new();

    // ExtTender_Settings
    private string _salesTenderCods = string.Empty;
    private string _paymentsTenderCods = string.Empty;
    private string _ncTenderCods = string.Empty;
    private string _ncPaymentCods = string.Empty;
    private string _ncPaymentChargeCode = string.Empty;
    private int _tenderSettingsId;

    public bool IsBusy
    {
        get => _isBusy;
        private set { if (_isBusy != value) { _isBusy = value; OnPropertyChanged(); } }
    }

    public bool IsSaving
    {
        get => _isSaving;
        private set
        {
            if (_isSaving != value)
            {
                _isSaving = value;
                OnPropertyChanged();
                OnPropertyChanged(nameof(OperacionResumen));
            }
        }
    }

    public string StatusMessage
    {
        get => _statusMessage;
        private set { if (_statusMessage != value) { _statusMessage = value; OnPropertyChanged(); OnPropertyChanged(nameof(HasStatus)); } }
    }

    public bool HasStatus => !string.IsNullOrWhiteSpace(StatusMessage);
    public bool HasParametros => Parametros.Count > 0;
    public int ParametrosCount => Parametros.Count;
    public int TendersConfiguradosCount => CountConfiguredTenders();
    public string DashboardResumen => $"{ParametrosCount} parámetros generales y {TendersConfiguradosCount}/3 grupos tender configurados.";
    public string OperacionResumen => IsSaving ? "Guardando cambios..." : "Use una sección a la vez para trabajar con más espacio.";
    public bool IsParametrosSectionActive => _activeSection == "Parametros";
    public bool IsTendersSectionActive => _activeSection == "Tenders";

    public string SalesTenderCods
    {
        get => _salesTenderCods;
        set { if (_salesTenderCods != value) { _salesTenderCods = value; OnPropertyChanged(); OnPropertyChanged(nameof(TendersConfiguradosCount)); OnPropertyChanged(nameof(DashboardResumen)); } }
    }

    public string PaymentsTenderCods
    {
        get => _paymentsTenderCods;
        set { if (_paymentsTenderCods != value) { _paymentsTenderCods = value; OnPropertyChanged(); OnPropertyChanged(nameof(TendersConfiguradosCount)); OnPropertyChanged(nameof(DashboardResumen)); } }
    }

    public string NCTenderCods
    {
        get => _ncTenderCods;
        set { if (_ncTenderCods != value) { _ncTenderCods = value; OnPropertyChanged(); OnPropertyChanged(nameof(TendersConfiguradosCount)); OnPropertyChanged(nameof(DashboardResumen)); } }
    }

    public string NCPaymentCods
    {
        get => _ncPaymentCods;
        set { if (_ncPaymentCods != value) { _ncPaymentCods = value; OnPropertyChanged(); OnPropertyChanged(nameof(TendersConfiguradosCount)); OnPropertyChanged(nameof(DashboardResumen)); } }
    }

    public string NCPaymentChargeCode
    {
        get => _ncPaymentChargeCode;
        set { if (_ncPaymentChargeCode != value) { _ncPaymentChargeCode = value; OnPropertyChanged(); OnPropertyChanged(nameof(TendersConfiguradosCount)); OnPropertyChanged(nameof(DashboardResumen)); } }
    }

    public ICommand SaveParametroCommand { get; }
    public ICommand SaveTenderSettingsCommand { get; }
    public ICommand GoBackCommand { get; }
    public ICommand ShowParametrosSectionCommand { get; }
    public ICommand ShowTendersSectionCommand { get; }

    public ParametrosViewModel(IParametrosService service, IStoreConfigService storeConfig, IDialogService dialog)
    {
        _service = service;
        _storeConfig = storeConfig;
        _dialog = dialog;
        Parametros.CollectionChanged += (_, _) =>
        {
            OnPropertyChanged(nameof(HasParametros));
            OnPropertyChanged(nameof(ParametrosCount));
            OnPropertyChanged(nameof(DashboardResumen));
        };

        SaveParametroCommand = new Command<ParametroEditItem>(async item => await SaveParametroAsync(item));
        SaveTenderSettingsCommand = new Command(async () => await SaveTenderSettingsAsync());
        GoBackCommand = new Command(async () => await Shell.Current.GoToAsync(".."));
        ShowParametrosSectionCommand = new Command(() => SetActiveSection("Parametros"));
        ShowTendersSectionCommand = new Command(() => SetActiveSection("Tenders"));
    }

    public async Task LoadAsync()
    {
        if (IsBusy) return;
        IsBusy = true;
        StatusMessage = string.Empty;

        try
        {
            var parametrosError = string.Empty;
            var tenderError = string.Empty;

            List<ParametroModel> parametros;
            try
            {
                parametros = await _service.GetParametrosAsync();
            }
            catch (Exception ex)
            {
                parametros = [];
                parametrosError = ex.Message;
            }

            try
            {
                await MainThread.InvokeOnMainThreadAsync(() =>
                {
                    Parametros.Clear();
                    foreach (var p in parametros)
                    {
                        // CAT-01 se maneja per-user en la config de categorías, no aquí
                        if (string.Equals(p.Codigo, "CAT-01", StringComparison.OrdinalIgnoreCase))
                            continue;

                        Parametros.Add(new ParametroEditItem
                        {
                            Codigo = p.Codigo,
                            Descripcion = p.Descripcion,
                            Valor = p.Valor,
                            OriginalValor = p.Valor
                        });
                    }
                });
            }
            catch (Exception ex)
            {
                parametrosError = string.IsNullOrWhiteSpace(parametrosError)
                    ? ex.Message
                    : parametrosError;
            }

            TenderSettingsModel? tender = null;
            try
            {
                tender = await _service.GetTenderSettingsAsync();
            }
            catch (Exception ex)
            {
                tenderError = ex.Message;
            }

            if (tender is not null)
            {
                _tenderSettingsId = tender.ID;
                SalesTenderCods = tender.SalesTenderCods;
                PaymentsTenderCods = tender.PaymentsTenderCods;
                NCTenderCods = tender.NCTenderCods;
                NCPaymentCods = tender.NCPaymentCods;
                NCPaymentChargeCode = tender.NCPaymentChargeCode;
            }

            // Cargar lista de tenders con checkboxes
            try
            {
                var allTenders = await _storeConfig.GetTendersAsync();
                var salesIds = ParseIds(SalesTenderCods);
                var payIds = ParseIds(PaymentsTenderCods);
                var ncIds = ParseIds(NCTenderCods);

                await MainThread.InvokeOnMainThreadAsync(() =>
                {
                    TenderOptions.Clear();
                    foreach (var t in allTenders)
                    {
                        var item = new TenderCheckItem
                        {
                            ID = t.ID,
                            Description = t.Description,
                            IsForSales = salesIds.Contains(t.ID),
                            IsForPayments = payIds.Contains(t.ID),
                            IsForNC = ncIds.Contains(t.ID)
                        };
                        item.CheckChanged = () => SyncTenderCods();
                        TenderOptions.Add(item);
                    }
                });
            }
            catch { /* no crítico */ }

            if (parametros.Count > 0 && string.IsNullOrWhiteSpace(tenderError))
            {
                StatusMessage = $"{parametros.Count} parámetros cargados.";
            }
            else if (parametros.Count == 0 && tender is not null)
            {
                StatusMessage = string.IsNullOrWhiteSpace(parametrosError)
                    ? "Los tenders cargaron, pero no llegaron parámetros generales."
                    : $"Parámetros: {parametrosError}";
            }
            else if (!string.IsNullOrWhiteSpace(parametrosError) || !string.IsNullOrWhiteSpace(tenderError))
            {
                StatusMessage = !string.IsNullOrWhiteSpace(parametrosError)
                    ? $"Parámetros: {parametrosError}"
                    : $"Tenders: {tenderError}";
            }
        }
        catch (Exception ex)
        {
            StatusMessage = $"Error al cargar los parámetros: {ex.Message}";
        }
        finally
        {
            IsBusy = false;
        }
    }

    private async Task SaveParametroAsync(ParametroEditItem item)
    {
        if (item is null) return;

        IsSaving = true;
        StatusMessage = string.Empty;

        try
        {
            var model = new ParametroModel
            {
                Codigo = item.Codigo,
                Descripcion = item.Descripcion,
                Valor = item.Valor
            };

            var ok = await _service.SaveParametroAsync(model);
            if (ok)
            {
                item.OriginalValor = item.Valor;
                item.NotifyChanged();
                StatusMessage = $"Parámetro {item.Codigo} guardado.";
                ParametrosChanged.Send();
                await _dialog.AlertAsync("Parámetro Actualizado",
                    $"El parámetro {item.Codigo} ({item.Descripcion}) se guardó correctamente.\n\nNuevo valor: {item.Valor}", "Aceptar");
            }
            else
            {
                StatusMessage = $"Error al guardar {item.Codigo}.";
                await _dialog.AlertAsync("Error", $"No se pudo guardar el parámetro {item.Codigo}. Intente de nuevo.", "Aceptar");
            }
        }
        catch
        {
            StatusMessage = $"Error al guardar {item.Codigo}.";
        }
        finally
        {
            IsSaving = false;
        }
    }

    private async Task SaveTenderSettingsAsync()
    {
        IsSaving = true;
        StatusMessage = string.Empty;

        try
        {
            var model = new TenderSettingsModel
            {
                ID = _tenderSettingsId > 0 ? _tenderSettingsId : 1,
                SalesTenderCods = SalesTenderCods,
                PaymentsTenderCods = PaymentsTenderCods,
                NCTenderCods = NCTenderCods,
                NCPaymentCods = NCPaymentCods,
                NCPaymentChargeCode = NCPaymentChargeCode
            };

            var ok = await _service.SaveTenderSettingsAsync(model);
            if (ok)
            {
                TenderSettingsChanged.Send();
                StatusMessage = "Configuración de tenders guardada.";
                await _dialog.AlertAsync("Tenders Actualizados",
                    "La configuración de formas de pago se guardó correctamente.", "Aceptar");
            }
            else
            {
                StatusMessage = "Error al guardar tenders.";
                await _dialog.AlertAsync("Error", "No se pudo guardar la configuración de tenders. Intente de nuevo.", "Aceptar");
            }
        }
        catch
        {
            StatusMessage = "Error al guardar tenders.";
        }
        finally
        {
            IsSaving = false;
        }
    }

    public event PropertyChangedEventHandler? PropertyChanged;

    private int CountConfiguredTenders()
    {
        var values = new[]
        {
            SalesTenderCods,
            PaymentsTenderCods,
            NCTenderCods
        };

        return values.Count(value => !string.IsNullOrWhiteSpace(value));
    }

    private void SetActiveSection(string section)
    {
        if (_activeSection == section)
            return;

        _activeSection = section;
        OnPropertyChanged(nameof(IsParametrosSectionActive));
        OnPropertyChanged(nameof(IsTendersSectionActive));
    }

    private void SyncTenderCods()
    {
        SalesTenderCods = string.Join(",", TenderOptions.Where(t => t.IsForSales).Select(t => t.ID));
        PaymentsTenderCods = string.Join(",", TenderOptions.Where(t => t.IsForPayments).Select(t => t.ID));
        NCTenderCods = string.Join(",", TenderOptions.Where(t => t.IsForNC).Select(t => t.ID));
    }

    private static HashSet<int> ParseIds(string? codes)
    {
        if (string.IsNullOrWhiteSpace(codes))
            return [];
        var set = new HashSet<int>();
        foreach (var part in codes.Split(new[] { ',', '_' }, StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries))
        {
            if (int.TryParse(part, out var id))
                set.Add(id);
        }
        return set;
    }

    private void OnPropertyChanged([CallerMemberName] string? name = null)
        => PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(name));
}

public class TenderCheckItem : INotifyPropertyChanged
{
    private bool _isForSales;
    private bool _isForPayments;
    private bool _isForNC;
    private bool _isForNCPayment;

    public int ID { get; set; }
    public string Description { get; set; } = string.Empty;
    public Action? CheckChanged { get; set; }

    public bool IsForSales
    {
        get => _isForSales;
        set { if (_isForSales != value) { _isForSales = value; OnPropertyChanged(); CheckChanged?.Invoke(); } }
    }

    public bool IsForPayments
    {
        get => _isForPayments;
        set { if (_isForPayments != value) { _isForPayments = value; OnPropertyChanged(); CheckChanged?.Invoke(); } }
    }

    public bool IsForNC
    {
        get => _isForNC;
        set { if (_isForNC != value) { _isForNC = value; OnPropertyChanged(); CheckChanged?.Invoke(); } }
    }

    public bool IsForNCPayment
    {
        get => _isForNCPayment;
        set { if (_isForNCPayment != value) { _isForNCPayment = value; OnPropertyChanged(); CheckChanged?.Invoke(); } }
    }

    public event PropertyChangedEventHandler? PropertyChanged;
    private void OnPropertyChanged([CallerMemberName] string? name = null)
        => PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(name));
}

public class ParametroEditItem : INotifyPropertyChanged
{
    private string _valor = string.Empty;

    public string Codigo { get; set; } = string.Empty;
    public string Descripcion { get; set; } = string.Empty;
    public string OriginalValor { get; set; } = string.Empty;

    public string Valor
    {
        get => _valor;
        set { if (_valor != value) { _valor = value; OnPropertyChanged(); OnPropertyChanged(nameof(IsModified)); } }
    }

    public bool IsModified => !string.Equals(Valor, OriginalValor, StringComparison.Ordinal);

    public void NotifyChanged()
    {
        OnPropertyChanged(nameof(IsModified));
    }

    public event PropertyChangedEventHandler? PropertyChanged;
    private void OnPropertyChanged([CallerMemberName] string? name = null)
        => PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(name));
}
