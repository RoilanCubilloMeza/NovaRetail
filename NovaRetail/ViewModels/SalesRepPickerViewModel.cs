using NovaRetail.Models;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Runtime.CompilerServices;
using System.Windows.Input;

namespace NovaRetail.ViewModels;

public class SalesRepPickerViewModel : INotifyPropertyChanged
{
    private readonly List<SalesRepModel> _allReps = [];
    private string _searchText = string.Empty;
    private SalesRepModel? _selectedRep;
    private bool _isBusy;
    private bool _canSkip = true;
    private string _title = "Seleccionar Vendedor";
    private string _subtitle = "Busque y seleccione el vendedor para esta sesión.";

    public ObservableCollection<SalesRepModel> FilteredReps { get; } = [];

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

    public string SearchText
    {
        get => _searchText;
        set
        {
            if (_searchText != value)
            {
                _searchText = value;
                OnPropertyChanged();
                ApplyFilter();
            }
        }
    }

    public SalesRepModel? SelectedRep
    {
        get => _selectedRep;
        set
        {
            if (_selectedRep != value)
            {
                _selectedRep = value;
                OnPropertyChanged();
                OnPropertyChanged(nameof(HasSelection));
                OnPropertyChanged(nameof(SelectionText));
                ((Command)ConfirmCommand).ChangeCanExecute();
            }
        }
    }

    public bool HasSelection => _selectedRep is not null;
    public string SelectionText => _selectedRep is not null
        ? $"Vendedor: {_selectedRep.Nombre}"
        : "Ninguno seleccionado";

    public bool IsBusy
    {
        get => _isBusy;
        private set { if (_isBusy != value) { _isBusy = value; OnPropertyChanged(); } }
    }

    public bool CanSkip
    {
        get => _canSkip;
        private set
        {
            if (_canSkip != value)
            {
                _canSkip = value;
                OnPropertyChanged();
                ((Command)SkipCommand).ChangeCanExecute();
            }
        }
    }

    public event Action<SalesRepModel>? RequestConfirm;
    public event Action? RequestSkip;

    public ICommand SelectRepCommand { get; }
    public ICommand ConfirmCommand { get; }
    public ICommand SkipCommand { get; }

    public SalesRepPickerViewModel()
    {
        SelectRepCommand = new Command<SalesRepModel>(rep => SelectedRep = rep);
        ConfirmCommand = new Command(() => { if (_selectedRep is not null) RequestConfirm?.Invoke(_selectedRep); }, () => HasSelection);
        SkipCommand = new Command(() => RequestSkip?.Invoke(), () => CanSkip);
    }

    public void Load(IEnumerable<SalesRepModel> reps, bool canSkip, string? title = null, string? subtitle = null)
    {
        CanSkip = canSkip;
        if (title is not null) Title = title;
        if (subtitle is not null) Subtitle = subtitle;

        _allReps.Clear();
        _allReps.AddRange(reps);
        _selectedRep = null;
        _searchText = string.Empty;
        OnPropertyChanged(nameof(SearchText));
        OnPropertyChanged(nameof(SelectedRep));
        OnPropertyChanged(nameof(HasSelection));
        OnPropertyChanged(nameof(SelectionText));
        ((Command)ConfirmCommand).ChangeCanExecute();
        ApplyFilter();
    }

    private void ApplyFilter()
    {
        FilteredReps.Clear();
        var query = (_searchText ?? string.Empty).Trim();
        var source = string.IsNullOrEmpty(query)
            ? _allReps
            : _allReps.Where(r =>
                r.Nombre.Contains(query, StringComparison.OrdinalIgnoreCase) ||
                r.Number.Contains(query, StringComparison.OrdinalIgnoreCase)).ToList();

        foreach (var r in source)
            FilteredReps.Add(r);
    }

    public event PropertyChangedEventHandler? PropertyChanged;
    protected void OnPropertyChanged([CallerMemberName] string? name = null)
        => PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(name));
}
