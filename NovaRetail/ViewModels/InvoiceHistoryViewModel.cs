using Microsoft.Extensions.DependencyInjection;
using NovaRetail.Data;
using NovaRetail.Models;
using NovaRetail.Services;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Runtime.CompilerServices;
using System.Windows.Input;

namespace NovaRetail.ViewModels;

public sealed class InvoiceHistoryViewModel : INotifyPropertyChanged
{
    private readonly IInvoiceHistoryService _historyService;
    private readonly IDialogService _dialogService;
    private readonly ISaleService _saleService;
    private readonly List<InvoiceHistoryEntry> _localEntries = new();
    private readonly List<InvoiceHistoryEntry> _remoteEntries = new();
    private readonly Dictionary<string, List<InvoiceHistoryEntry>> _remoteSearchCache = new(StringComparer.OrdinalIgnoreCase);

    private string _searchText = string.Empty;
    private bool _isLoading;
    private InvoiceHistoryEntry? _selectedEntry;
    private bool _isReprintVisible;
    private CancellationTokenSource? _searchCts;
    private string _lastRemoteSearch = string.Empty;

    public ObservableCollection<InvoiceHistoryEntry> Entries { get; } = new();
    public ReceiptViewModel ReprintVm { get; } = new();

    public string SearchText
    {
        get => _searchText;
        set
        {
            if (_searchText == value) return;
            _searchText = value;
            OnPropertyChanged();
            OnPropertyChanged(nameof(LoadingMessageText));
            OnPropertyChanged(nameof(ResultSummaryText));
            _ = RefreshSearchAsync();
        }
    }

    public bool IsLoading
    {
        get => _isLoading;
        private set
        {
            if (_isLoading == value) return;
            _isLoading = value;
            OnPropertyChanged();
            OnPropertyChanged(nameof(IsNotLoading));
            OnPropertyChanged(nameof(IsEmpty));
            OnPropertyChanged(nameof(HasEntries));
            OnPropertyChanged(nameof(IsCompletelyEmpty));
            OnPropertyChanged(nameof(IsSearchEmpty));
            OnPropertyChanged(nameof(LoadingMessageText));
            OnPropertyChanged(nameof(ResultSummaryText));
        }
    }

    public bool IsNotLoading => !_isLoading;

    public bool IsEmpty => Entries.Count == 0 && !_isLoading;
    public bool HasEntries => Entries.Count > 0;
    public bool IsSearchActive => !string.IsNullOrWhiteSpace(_searchText);
    public bool IsCompletelyEmpty => !IsSearchActive && Entries.Count == 0 && !_isLoading;
    public bool IsSearchEmpty => IsSearchActive && Entries.Count == 0 && !_isLoading;
    public bool HasLocalEntries => _localEntries.Count > 0;
    public string LoadingMessageText => IsSearchActive
        ? "Buscando facturas..."
        : "Cargando facturas pasadas...";
    public string ResultSummaryText => IsLoading
        ? LoadingMessageText
        : HasEntries
            ? $"{Entries.Count} factura(s) encontradas"
            : IsSearchActive
                ? "Sin coincidencias en historial local o servidor"
                : "Facturas recientes del dispositivo y del servidor";

    public InvoiceHistoryEntry? SelectedEntry
    {
        get => _selectedEntry;
        set { _selectedEntry = value; OnPropertyChanged(); OnPropertyChanged(nameof(HasSelectedEntry)); }
    }

    public bool HasSelectedEntry => _selectedEntry is not null;

    public bool IsReprintVisible
    {
        get => _isReprintVisible;
        private set { if (_isReprintVisible == value) return; _isReprintVisible = value; OnPropertyChanged(); }
    }

    public ICommand LoadCommand { get; }
    public ICommand DeleteCommand { get; }
    public ICommand ClearAllCommand { get; }
    public ICommand SelectEntryCommand { get; }
    public ICommand CloseDetailCommand { get; }
    public ICommand ReprintCommand { get; }
    public ICommand CreditNoteCommand { get; }
    public ICommand StandaloneCreditNoteCommand { get; }

    public InvoiceHistoryViewModel(IInvoiceHistoryService historyService, IDialogService dialogService, ISaleService saleService)
    {
        _historyService = historyService;
        _dialogService = dialogService;
        _saleService = saleService;

        LoadCommand = new Command(async () => await LoadAsync());
        DeleteCommand = new Command<InvoiceHistoryEntry>(async e => await DeleteAsync(e));
        ClearAllCommand = new Command(async () => await ClearAllAsync());
        SelectEntryCommand = new Command<InvoiceHistoryEntry>(async e => await SelectEntryAsync(e));
        CloseDetailCommand = new Command(() => SelectedEntry = null);
        ReprintCommand = new Command<InvoiceHistoryEntry>(async e => await ShowReprintAsync(e));
        CreditNoteCommand = new Command<InvoiceHistoryEntry>(async e => await NavigateToCreditNoteAsync(e));
        StandaloneCreditNoteCommand = new Command(async () => await StandaloneCreditNoteAsync());

        ReprintVm.RequestClose += () => IsReprintVisible = false;
    }

    public async Task LoadAsync()
    {
        _searchCts?.Cancel();
        IsLoading = true;
        try
        {
            var list = await _historyService.GetAllAsync();
            _localEntries.Clear();
            _localEntries.AddRange(list.Select(entry =>
            {
                entry.IsLocalEntry = true;
                return entry;
            }));

            await LoadRemoteEntriesAsync(NormalizeSearch(_searchText), CancellationToken.None, forceRefresh: true);
            ApplyFilter();
        }
        finally
        {
            IsLoading = false;
            OnPropertyChanged(nameof(IsEmpty));
            OnPropertyChanged(nameof(HasEntries));
            OnPropertyChanged(nameof(IsCompletelyEmpty));
            OnPropertyChanged(nameof(IsSearchEmpty));
            OnPropertyChanged(nameof(HasLocalEntries));
            OnPropertyChanged(nameof(LoadingMessageText));
            OnPropertyChanged(nameof(ResultSummaryText));
        }
    }

    private async Task RefreshSearchAsync()
    {
        _searchCts?.Cancel();
        var cts = new CancellationTokenSource();
        _searchCts = cts;

        try
        {
            ApplyFilter();

            var normalizedSearch = NormalizeSearch(_searchText);
            OnPropertyChanged(nameof(LoadingMessageText));
            if (!ShouldQueryRemote(normalizedSearch))
                return;

            IsLoading = true;
            await Task.Delay(string.IsNullOrWhiteSpace(normalizedSearch) ? 0 : 450, cts.Token);
            await LoadRemoteEntriesAsync(normalizedSearch, cts.Token);
            ApplyFilter();
        }
        catch (OperationCanceledException)
        {
        }
        finally
        {
            if (_searchCts == cts)
            {
                IsLoading = false;
                OnPropertyChanged(nameof(IsEmpty));
                OnPropertyChanged(nameof(HasEntries));
                OnPropertyChanged(nameof(IsCompletelyEmpty));
                OnPropertyChanged(nameof(IsSearchEmpty));
                OnPropertyChanged(nameof(LoadingMessageText));
                OnPropertyChanged(nameof(ResultSummaryText));
            }
        }
    }

    private async Task SelectEntryAsync(InvoiceHistoryEntry? entry)
    {
        if (entry is null)
            return;

        SelectedEntry = await EnsureDetailAsync(entry);
    }

    private async Task ShowReprintAsync(InvoiceHistoryEntry? entry)
    {
        if (entry is null)
            return;

        var fullEntry = await EnsureDetailAsync(entry);
        ReprintVm.LoadFromHistory(fullEntry);
        IsReprintVisible = true;
    }

    private async Task NavigateToCreditNoteAsync(InvoiceHistoryEntry? entry)
    {
        if (entry is null)
            return;

        // Only allow NC for facturas/tiquetes, not for existing NCs
        if (entry.ComprobanteTipo == "03")
        {
            await _dialogService.AlertAsync("Nota de Crédito", "No se puede crear una nota de crédito sobre otra nota de crédito.", "OK");
            return;
        }

        var fullEntry = await EnsureDetailAsync(entry);
        if (fullEntry.Lines.Count == 0)
        {
            await _dialogService.AlertAsync("Nota de Crédito", "No se encontraron líneas de detalle para esta factura.", "OK");
            return;
        }

        // Navigate to CreditNotePage passing the entry via the service provider
        var page = Application.Current?.Handler?.MauiContext?.Services.GetService<NovaRetail.Pages.CreditNotePage>();
        if (page is null)
            return;

        await page.LoadAsync(fullEntry);
        await Shell.Current.Navigation.PushAsync(page);
    }

    private async Task StandaloneCreditNoteAsync()
    {
        var clave50 = await _dialogService.PromptAsync(
            "NC por Clave 50",
            "Ingrese la Clave 50 de referencia (máx. 50 caracteres):",
            "Continuar", "Cancelar",
            placeholder: "Clave 50...",
            maxLength: 50);

        if (string.IsNullOrWhiteSpace(clave50))
            return;

        clave50 = clave50.Trim();
        if (clave50.Length > 50)
        {
            await _dialogService.AlertAsync("Error", "La clave no puede tener más de 50 caracteres.", "OK");
            return;
        }

        // Try to find the invoice by Clave50 in the server
        InvoiceHistoryEntry? foundEntry = null;
        try
        {
            var result = await _saleService.SearchInvoiceHistoryAsync(clave50, CancellationToken.None);
            if (result.Ok && result.Entries.Count > 0)
            {
                var match = result.Entries.FirstOrDefault(e =>
                    !string.IsNullOrWhiteSpace(e.Clave50) &&
                    string.Equals(e.Clave50, clave50, StringComparison.OrdinalIgnoreCase));

                if (match is not null)
                {
                    foundEntry = MapRemoteEntry(match);
                    foundEntry = await EnsureDetailAsync(foundEntry);
                }
            }
        }
        catch
        {
            // If search fails, continue with standalone mode
        }

        var page = Application.Current?.Handler?.MauiContext?.Services
            .GetService<NovaRetail.Pages.CreditNotePage>();
        if (page is null)
            return;

        if (foundEntry is not null && foundEntry.Lines.Count > 0)
        {
            // Found the invoice - use normal NC flow
            await page.LoadAsync(foundEntry);
        }
        else
        {
            // Standalone NC - no source invoice found
            await page.LoadStandaloneAsync(clave50);
        }

        await Shell.Current.Navigation.PushAsync(page);
    }

    private async Task LoadRemoteEntriesAsync(string search, CancellationToken cancellationToken, bool forceRefresh = false)
    {
        if (!forceRefresh && string.Equals(_lastRemoteSearch, search, StringComparison.OrdinalIgnoreCase))
            return;

        if (!forceRefresh && _remoteSearchCache.TryGetValue(search, out var cachedEntries))
        {
            _remoteEntries.Clear();
            _remoteEntries.AddRange(cachedEntries.Select(CloneEntry));
            _lastRemoteSearch = search;
            return;
        }

        var result = await _saleService.SearchInvoiceHistoryAsync(search, cancellationToken);

        _remoteEntries.Clear();
        if (!result.Ok || result.Entries.Count == 0)
        {
            _remoteSearchCache[search] = new List<InvoiceHistoryEntry>();
            _lastRemoteSearch = search;
            return;
        }

        var mappedEntries = result.Entries.Select(MapRemoteEntry).ToList();
        _remoteEntries.AddRange(mappedEntries.Select(CloneEntry));
        _remoteSearchCache[search] = mappedEntries;
        _lastRemoteSearch = search;
    }

    private async Task<InvoiceHistoryEntry> EnsureDetailAsync(InvoiceHistoryEntry entry)
    {
        if (entry.IsLocalEntry || entry.Lines.Count > 0)
            return entry;

        var result = await _saleService.GetInvoiceHistoryDetailAsync(entry.TransactionNumber);
        if (!result.Ok || result.Entry is null)
            return entry;

        var detailedEntry = MapRemoteEntry(result.Entry);
        ReplaceRemoteEntry(detailedEntry);
        return detailedEntry;
    }

    private void ReplaceRemoteEntry(InvoiceHistoryEntry detailedEntry)
    {
        var index = _remoteEntries.FindIndex(x => x.TransactionNumber == detailedEntry.TransactionNumber);
        if (index >= 0)
            _remoteEntries[index] = detailedEntry;

        foreach (var cacheEntry in _remoteSearchCache.Values)
        {
            var cacheIndex = cacheEntry.FindIndex(x => x.TransactionNumber == detailedEntry.TransactionNumber);
            if (cacheIndex >= 0)
                cacheEntry[cacheIndex] = CloneEntry(detailedEntry);
        }

        var currentIndex = Entries.ToList().FindIndex(x => x.TransactionNumber == detailedEntry.TransactionNumber && !x.IsLocalEntry);
        if (currentIndex >= 0)
            Entries[currentIndex] = detailedEntry;
    }

    private static string NormalizeSearch(string search) => (search ?? string.Empty).Trim();

    private static bool ShouldQueryRemote(string search)
    {
        if (string.IsNullOrWhiteSpace(search))
            return true;

        return search.Length >= 3;
    }

    private static InvoiceHistoryEntry CloneEntry(InvoiceHistoryEntry entry)
    {
        return new InvoiceHistoryEntry
        {
            Id = entry.Id,
            Date = entry.Date,
            IsLocalEntry = entry.IsLocalEntry,
            TransactionNumber = entry.TransactionNumber,
            ComprobanteTipo = entry.ComprobanteTipo,
            Clave50 = entry.Clave50,
            Consecutivo = entry.Consecutivo,
            ClientId = entry.ClientId,
            ClientName = entry.ClientName,
            CashierName = entry.CashierName,
            RegisterNumber = entry.RegisterNumber,
            StoreName = entry.StoreName,
            SubtotalColones = entry.SubtotalColones,
            DiscountColones = entry.DiscountColones,
            ExonerationColones = entry.ExonerationColones,
            TaxColones = entry.TaxColones,
            TotalColones = entry.TotalColones,
            ChangeColones = entry.ChangeColones,
            TenderDescription = entry.TenderDescription,
            TenderTotalColones = entry.TenderTotalColones,
            SecondTenderDescription = entry.SecondTenderDescription,
            SecondTenderAmountColones = entry.SecondTenderAmountColones,
            Lines = entry.Lines.Select(line => new InvoiceHistoryLine
            {
                ItemID = line.ItemID,
                TaxID = line.TaxID,
                DisplayName = line.DisplayName,
                Code = line.Code,
                Quantity = line.Quantity,
                TaxPercentage = line.TaxPercentage,
                UnitPriceColones = line.UnitPriceColones,
                LineTotalColones = line.LineTotalColones,
                HasDiscount = line.HasDiscount,
                DiscountPercent = line.DiscountPercent,
                HasExoneration = line.HasExoneration,
                ExonerationPercent = line.ExonerationPercent,
                HasOverridePrice = line.HasOverridePrice
            }).ToList()
        };
    }

    private static InvoiceHistoryEntry MapRemoteEntry(NovaRetailInvoiceHistoryEntryDto entry)
    {
        return new InvoiceHistoryEntry
        {
            IsLocalEntry = false,
            Date = entry.Date,
            TransactionNumber = entry.TransactionNumber,
            ComprobanteTipo = string.IsNullOrWhiteSpace(entry.ComprobanteTipo) ? "04" : entry.ComprobanteTipo,
            Clave50 = entry.Clave50 ?? string.Empty,
            Consecutivo = entry.Consecutivo ?? string.Empty,
            ClientId = entry.ClientId ?? string.Empty,
            ClientName = string.IsNullOrWhiteSpace(entry.ClientName) ? "CLIENTE CONTADO" : entry.ClientName,
            CashierName = entry.CashierName ?? string.Empty,
            RegisterNumber = entry.RegisterNumber,
            StoreName = entry.StoreName ?? string.Empty,
            SubtotalColones = entry.SubtotalColones,
            DiscountColones = entry.DiscountColones,
            ExonerationColones = entry.ExonerationColones,
            TaxColones = entry.TaxColones,
            TotalColones = entry.TotalColones,
            ChangeColones = entry.ChangeColones,
            TenderDescription = entry.TenderDescription ?? string.Empty,
            TenderTotalColones = entry.TenderTotalColones,
            SecondTenderDescription = entry.SecondTenderDescription ?? string.Empty,
            SecondTenderAmountColones = entry.SecondTenderAmountColones,
            Lines = entry.Lines.Select(line => new InvoiceHistoryLine
            {
                ItemID = line.ItemID,
                TaxID = line.TaxID,
                DisplayName = line.DisplayName ?? string.Empty,
                Code = line.Code ?? string.Empty,
                Quantity = line.Quantity,
                TaxPercentage = line.TaxPercentage,
                UnitPriceColones = line.UnitPriceColones,
                LineTotalColones = line.LineTotalColones,
                HasDiscount = line.HasDiscount,
                DiscountPercent = line.DiscountPercent,
                HasExoneration = line.HasExoneration,
                ExonerationPercent = line.ExonerationPercent,
                HasOverridePrice = line.HasOverridePrice
            }).ToList()
        };
    }

    private void ApplyFilter()
    {
        var term = _searchText.Trim();
        var localFiltered = string.IsNullOrWhiteSpace(term)
            ? _localEntries
            : _localEntries.Where(e =>
                e.ClientName.Contains(term, StringComparison.OrdinalIgnoreCase) ||
                e.ClientId.Contains(term, StringComparison.OrdinalIgnoreCase) ||
                e.TransactionNumber.ToString().Contains(term) ||
                e.Consecutivo.Contains(term, StringComparison.OrdinalIgnoreCase) ||
                e.DocumentTypeName.Contains(term, StringComparison.OrdinalIgnoreCase))
            .ToList();

        var remoteFiltered = string.IsNullOrWhiteSpace(term)
            ? _remoteEntries
            : _remoteEntries.Where(e =>
                e.ClientName.Contains(term, StringComparison.OrdinalIgnoreCase) ||
                e.ClientId.Contains(term, StringComparison.OrdinalIgnoreCase) ||
                e.TransactionNumber.ToString().Contains(term) ||
                e.Consecutivo.Contains(term, StringComparison.OrdinalIgnoreCase) ||
                e.DocumentTypeName.Contains(term, StringComparison.OrdinalIgnoreCase))
            .ToList();

        var filtered = localFiltered
            .Concat(remoteFiltered)
            .GroupBy(e => e.TransactionNumber > 0 ? $"TN:{e.TransactionNumber}" : $"ID:{e.Id}")
            .Select(g => g.OrderByDescending(x => x.IsLocalEntry).First())
            .OrderByDescending(e => e.Date)
            .ToList();

        Entries.Clear();
        foreach (var e in filtered)
            Entries.Add(e);

        OnPropertyChanged(nameof(IsEmpty));
        OnPropertyChanged(nameof(HasEntries));
        OnPropertyChanged(nameof(IsSearchActive));
        OnPropertyChanged(nameof(IsCompletelyEmpty));
        OnPropertyChanged(nameof(IsSearchEmpty));
        OnPropertyChanged(nameof(ResultSummaryText));
    }

    private async Task DeleteAsync(InvoiceHistoryEntry? entry)
    {
        if (entry is null || !entry.CanDelete) return;

        var confirmed = await _dialogService.ConfirmAsync(
            "Eliminar registro",
            $"¿Desea eliminar la factura #{entry.TransactionNumber}?",
            "Eliminar", "Cancelar");

        if (!confirmed) return;

        await _historyService.DeleteAsync(entry.Id);
        _localEntries.Remove(entry);
        Entries.Remove(entry);
        if (SelectedEntry?.Id == entry.Id)
            SelectedEntry = null;
        OnPropertyChanged(nameof(IsEmpty));
        OnPropertyChanged(nameof(HasEntries));
        OnPropertyChanged(nameof(IsCompletelyEmpty));
        OnPropertyChanged(nameof(IsSearchEmpty));
        OnPropertyChanged(nameof(HasLocalEntries));
        OnPropertyChanged(nameof(ResultSummaryText));
    }

    private async Task ClearAllAsync()
    {
        if (_localEntries.Count == 0) return;

        var confirmed = await _dialogService.ConfirmAsync(
            "Limpiar historial",
            "¿Desea eliminar todo el historial local de facturas?",
            "Limpiar", "Cancelar");

        if (!confirmed) return;

        _searchCts?.Cancel();
        await _historyService.ClearAllAsync();
        _localEntries.Clear();
        _searchText = string.Empty;
        OnPropertyChanged(nameof(SearchText));
        SelectedEntry = null;
        _remoteEntries.Clear();
        _remoteSearchCache.Clear();
        _lastRemoteSearch = string.Empty;
        Entries.Clear();
        await LoadAsync();
    }

    public event PropertyChangedEventHandler? PropertyChanged;
    private void OnPropertyChanged([CallerMemberName] string? name = null)
        => PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(name));
}
