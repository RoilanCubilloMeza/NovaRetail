using NovaRetail.Models;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Globalization;
using System.Runtime.CompilerServices;
using System.Windows.Input;

namespace NovaRetail.ViewModels;

public sealed class WorkOrderPartialPickupViewModel : INotifyPropertyChanged
{
    private string _title = "Recoger Parcial";
    private string _subtitle = string.Empty;
    private string _summaryText = string.Empty;
    private bool _canConfirm;

    public ObservableCollection<WorkOrderPartialPickupLineViewModel> Lines { get; } = [];

    public string Title
    {
        get => _title;
        private set { if (_title != value) { _title = value; OnPropertyChanged(); } }
    }

    public string Subtitle
    {
        get => _subtitle;
        private set { if (_subtitle != value) { _subtitle = value; OnPropertyChanged(); } }
    }

    public string SummaryText
    {
        get => _summaryText;
        private set { if (_summaryText != value) { _summaryText = value; OnPropertyChanged(); } }
    }

    public bool CanConfirm
    {
        get => _canConfirm;
        private set
        {
            if (_canConfirm != value)
            {
                _canConfirm = value;
                OnPropertyChanged();
                ((Command)ConfirmCommand).ChangeCanExecute();
            }
        }
    }

    public event Func<Task>? RequestConfirm;
    public event Action? RequestCancel;

    public ICommand ConfirmCommand { get; }
    public ICommand CancelCommand { get; }

    public WorkOrderPartialPickupViewModel()
    {
        ConfirmCommand = new Command(async () =>
        {
            if (RequestConfirm is not null)
                await RequestConfirm.Invoke();
        }, () => CanConfirm);

        CancelCommand = new Command(() => RequestCancel?.Invoke());
    }

    public void Load(int orderId, IEnumerable<CartItemModel> cartItems, NovaRetailOrderDetail orderDetail)
    {
        Title = "Recoger Parcial";
        Subtitle = $"Orden de trabajo #{orderId}. Indique cuánto se llevará ahora.";

        Lines.Clear();

        var currentCart = cartItems
            .Where(item => item.SourceOrderEntryID > 0)
            .GroupBy(item => item.SourceOrderEntryID)
            .ToDictionary(group => group.Key, group => group.First());

        foreach (var entry in orderDetail.Entries)
        {
            var availableQuantity = entry.QuantityOnOrder > 0m ? entry.QuantityOnOrder : 1m;
            if (availableQuantity <= 0m)
                continue;

            if (!currentCart.TryGetValue(entry.EntryID, out var cartItem))
                continue;

            var line = new WorkOrderPartialPickupLineViewModel(
                entryId: entry.EntryID,
                description: cartItem.DisplayName,
                code: cartItem.Code,
                availableQuantity: availableQuantity,
                selectedQuantity: Math.Min(Math.Max(cartItem.Quantity, 0m), availableQuantity));

            line.QuantityChanged += RefreshSummary;
            Lines.Add(line);
        }

        RefreshSummary();
    }

    public bool TryBuildSelection(out IReadOnlyDictionary<int, decimal> selection, out string validationMessage)
    {
        selection = Lines.ToDictionary(line => line.EntryID, line => line.SelectedQuantity);

        var totalAvailable = Lines.Sum(line => line.AvailableQuantity);
        var totalSelected = Lines.Sum(line => line.SelectedQuantity);

        if (Lines.Count == 0)
        {
            validationMessage = "No hay líneas disponibles para recoger parcialmente.";
            return false;
        }

        if (totalSelected <= 0m)
        {
            validationMessage = "Debe indicar al menos una cantidad mayor a cero para recoger.";
            return false;
        }

        if (totalSelected >= totalAvailable)
        {
            validationMessage = "La selección parcial debe dejar remanente en la orden. Si va a llevar todo, use Recoger Completo.";
            return false;
        }

        validationMessage = string.Empty;
        return true;
    }

    private void RefreshSummary()
    {
        var totalAvailable = Lines.Sum(line => line.AvailableQuantity);
        var totalSelected = Lines.Sum(line => line.SelectedQuantity);
        var remaining = Math.Max(0m, totalAvailable - totalSelected);

        CanConfirm = Lines.Count > 0 && totalSelected > 0m && remaining > 0m;

        if (Lines.Count == 0)
        {
            SummaryText = "No hay líneas disponibles para la selección parcial.";
            return;
        }

        if (totalSelected <= 0m)
        {
            SummaryText = $"Disponible: {FormatQuantity(totalAvailable)}. Indique cuánto se llevará ahora.";
            return;
        }

        if (remaining <= 0m)
        {
            SummaryText = "Debe dejar remanente en la orden. Si va a llevar todo, use Recoger Completo.";
            return;
        }

        SummaryText = $"Se llevará {FormatQuantity(totalSelected)} y quedarán {FormatQuantity(remaining)} en la orden.";
    }

    private static string FormatQuantity(decimal quantity)
        => quantity.ToString("0.##", CultureInfo.InvariantCulture);

    public event PropertyChangedEventHandler? PropertyChanged;

    private void OnPropertyChanged([CallerMemberName] string? propertyName = null)
        => PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
}

public sealed class WorkOrderPartialPickupLineViewModel : INotifyPropertyChanged
{
    private string _selectedQuantityText;
    private decimal _selectedQuantity;

    public int EntryID { get; }
    public string Description { get; }
    public string Code { get; }
    public decimal AvailableQuantity { get; }
    public decimal RemainingQuantity => Math.Max(0m, AvailableQuantity - _selectedQuantity);
    public string AvailableQuantityText => $"Disponible: {FormatQuantity(AvailableQuantity)}";
    public string RemainingQuantityText => $"Quedan en OT: {FormatQuantity(RemainingQuantity)}";
    public string CodeText => string.IsNullOrWhiteSpace(Code) ? string.Empty : $"Código: {Code}";

    public decimal SelectedQuantity
    {
        get => _selectedQuantity;
        private set
        {
            if (_selectedQuantity != value)
            {
                _selectedQuantity = value;
                OnPropertyChanged();
                OnPropertyChanged(nameof(RemainingQuantity));
                OnPropertyChanged(nameof(RemainingQuantityText));
                QuantityChanged?.Invoke();
            }
        }
    }

    public string SelectedQuantityText
    {
        get => _selectedQuantityText;
        set
        {
            if (_selectedQuantityText == value)
                return;

            _selectedQuantityText = value;
            OnPropertyChanged();
            ApplySelectedQuantity(value);
        }
    }

    public ICommand IncreaseCommand { get; }
    public ICommand DecreaseCommand { get; }

    public event Action? QuantityChanged;

    public WorkOrderPartialPickupLineViewModel(int entryId, string description, string code, decimal availableQuantity, decimal selectedQuantity)
    {
        EntryID = entryId;
        Description = string.IsNullOrWhiteSpace(description) ? "Artículo" : description;
        Code = code ?? string.Empty;
        AvailableQuantity = Math.Max(0m, availableQuantity);

        IncreaseCommand = new Command(() => ApplySelectedQuantity((_selectedQuantity + ResolveStep()).ToString("0.##", CultureInfo.InvariantCulture)));
        DecreaseCommand = new Command(() => ApplySelectedQuantity((_selectedQuantity - ResolveStep()).ToString("0.##", CultureInfo.InvariantCulture)));

        _selectedQuantityText = "0";
        ApplySelectedQuantity(selectedQuantity.ToString("0.##", CultureInfo.InvariantCulture));
    }

    private void ApplySelectedQuantity(string? rawValue)
    {
        var parsed = ParseQuantity(rawValue);
        var normalized = Math.Min(AvailableQuantity, Math.Max(0m, parsed));
        var normalizedText = FormatQuantity(normalized);

        if (_selectedQuantityText != normalizedText)
        {
            _selectedQuantityText = normalizedText;
            OnPropertyChanged(nameof(SelectedQuantityText));
        }

        SelectedQuantity = normalized;
    }

    private decimal ResolveStep()
        => AvailableQuantity % 1m == 0m ? 1m : 0.01m;

    private static decimal ParseQuantity(string? rawValue)
    {
        if (string.IsNullOrWhiteSpace(rawValue))
            return 0m;

        if (decimal.TryParse(rawValue, NumberStyles.Any, CultureInfo.CurrentCulture, out var currentCultureValue))
            return currentCultureValue;

        if (decimal.TryParse(rawValue, NumberStyles.Any, CultureInfo.InvariantCulture, out var invariantValue))
            return invariantValue;

        return 0m;
    }

    private static string FormatQuantity(decimal quantity)
        => quantity.ToString("0.##", CultureInfo.InvariantCulture);

    public event PropertyChangedEventHandler? PropertyChanged;

    private void OnPropertyChanged([CallerMemberName] string? propertyName = null)
        => PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
}