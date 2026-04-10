using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Runtime.CompilerServices;
using System.Windows.Input;
using Microsoft.Maui.ApplicationModel;
using NovaRetail.Data;
using NovaRetail.Models;
using NovaRetail.Services;

namespace NovaRetail.ViewModels;

public class ParametrosViewModel : INotifyPropertyChanged
{
    private readonly IParametrosService _service;
    private readonly IDialogService _dialog;

    private bool _isBusy;
    private bool _isSaving;
    private string _statusMessage = string.Empty;

    // Parámetros generales
    public ObservableCollection<ParametroEditItem> Parametros { get; } = new();

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
        private set { if (_isSaving != value) { _isSaving = value; OnPropertyChanged(); } }
    }

    public string StatusMessage
    {
        get => _statusMessage;
        private set { if (_statusMessage != value) { _statusMessage = value; OnPropertyChanged(); OnPropertyChanged(nameof(HasStatus)); } }
    }

    public bool HasStatus => !string.IsNullOrWhiteSpace(StatusMessage);
    public bool HasParametros => Parametros.Count > 0;

    public string SalesTenderCods
    {
        get => _salesTenderCods;
        set { if (_salesTenderCods != value) { _salesTenderCods = value; OnPropertyChanged(); } }
    }

    public string PaymentsTenderCods
    {
        get => _paymentsTenderCods;
        set { if (_paymentsTenderCods != value) { _paymentsTenderCods = value; OnPropertyChanged(); } }
    }

    public string NCTenderCods
    {
        get => _ncTenderCods;
        set { if (_ncTenderCods != value) { _ncTenderCods = value; OnPropertyChanged(); } }
    }

    public string NCPaymentCods
    {
        get => _ncPaymentCods;
        set { if (_ncPaymentCods != value) { _ncPaymentCods = value; OnPropertyChanged(); } }
    }

    public string NCPaymentChargeCode
    {
        get => _ncPaymentChargeCode;
        set { if (_ncPaymentChargeCode != value) { _ncPaymentChargeCode = value; OnPropertyChanged(); } }
    }

    public ICommand SaveParametroCommand { get; }
    public ICommand SaveTenderSettingsCommand { get; }
    public ICommand GoBackCommand { get; }

    public ParametrosViewModel(IParametrosService service, IDialogService dialog)
    {
        _service = service;
        _dialog = dialog;
        Parametros.CollectionChanged += (_, _) => OnPropertyChanged(nameof(HasParametros));

        SaveParametroCommand = new Command<ParametroEditItem>(async item => await SaveParametroAsync(item));
        SaveTenderSettingsCommand = new Command(async () => await SaveTenderSettingsAsync());
        GoBackCommand = new Command(async () => await Shell.Current.GoToAsync(".."));
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
                await _dialog.AlertAsync("✅ Parámetro Actualizado",
                    $"Se actualizó el parámetro '{item.Codigo}' ({item.Descripcion}).\n\nNuevo valor: {item.Valor}", "Aceptar");
            }
            else
            {
                StatusMessage = $"Error al guardar {item.Codigo}.";
                await _dialog.AlertAsync("❌ Error", $"No se pudo guardar el parámetro '{item.Codigo}'.", "Aceptar");
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
                StatusMessage = "Configuración de tenders guardada.";
                await _dialog.AlertAsync("✅ Tenders Actualizados",
                    "Se guardó correctamente la configuración de formas de pago.", "Aceptar");
            }
            else
            {
                StatusMessage = "Error al guardar tenders.";
                await _dialog.AlertAsync("❌ Error", "No se pudo guardar la configuración de tenders.", "Aceptar");
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
