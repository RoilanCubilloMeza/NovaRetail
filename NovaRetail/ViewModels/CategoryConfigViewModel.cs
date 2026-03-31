using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Runtime.CompilerServices;
using System.Windows.Input;
using NovaRetail.Data;
using NovaRetail.Models;
using NovaRetail.Services;

namespace NovaRetail.ViewModels;

public class CategoryConfigViewModel : INotifyPropertyChanged
{
    private const int MaxCategories = 6;

    private readonly IStoreConfigService _configService;
    private readonly IDialogService _dialogService;
    private readonly MainViewModel _mainVm;

    private bool _isBusy;
    private bool _isSaving;

    public ObservableCollection<SelectableCategoryItem> AllCategories { get; } = new();
    public ObservableCollection<SelectableCategoryItem> SelectedCategories { get; } = new();

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

    public int SelectedCount => SelectedCategories.Count;
    public string SelectedCountText => $"{SelectedCategories.Count} / {MaxCategories} seleccionadas";
    public bool CanAddMore => SelectedCategories.Count < MaxCategories;

    public ICommand ToggleCategoryCommand { get; }
    public ICommand MoveUpCommand { get; }
    public ICommand MoveDownCommand { get; }
    public ICommand RemoveCategoryCommand { get; }
    public ICommand SaveCommand { get; }
    public ICommand GoBackCommand { get; }

    public CategoryConfigViewModel(IStoreConfigService configService, IDialogService dialogService, MainViewModel mainVm)
    {
        _configService = configService;
        _dialogService = dialogService;
        _mainVm = mainVm;

        ToggleCategoryCommand = new Command<SelectableCategoryItem>(ToggleCategory);
        MoveUpCommand = new Command<SelectableCategoryItem>(MoveUp);
        MoveDownCommand = new Command<SelectableCategoryItem>(MoveDown);
        RemoveCategoryCommand = new Command<SelectableCategoryItem>(RemoveCategory);
        SaveCommand = new Command(async () => await SaveAsync());
        GoBackCommand = new Command(async () => await Shell.Current.GoToAsync(".."));
    }

    public async Task LoadAsync()
    {
        if (IsBusy) return;
        IsBusy = true;

        try
        {
            var allTask = _configService.GetAllCategoriesAsync();
            var configTask = _configService.GetCategoryConfigAsync();
            await Task.WhenAll(allTask, configTask);

            var allDepts = allTask.Result;
            var currentConfig = configTask.Result;

            var selectedIds = new HashSet<int>();
            if (!string.IsNullOrWhiteSpace(currentConfig))
            {
                foreach (var s in currentConfig.Split(',', StringSplitOptions.RemoveEmptyEntries))
                {
                    if (int.TryParse(s.Trim(), out var id))
                        selectedIds.Add(id);
                }
            }

            AllCategories.Clear();
            SelectedCategories.Clear();

            // Primero agregar las seleccionadas en orden del parámetro
            var deptMap = new Dictionary<int, CategoryModel>();
            foreach (var d in allDepts)
            {
                if (!deptMap.ContainsKey(d.ID))
                    deptMap[d.ID] = d;
            }
            foreach (var id in selectedIds)
            {
                if (deptMap.TryGetValue(id, out var dept))
                {
                    var item = new SelectableCategoryItem { ID = dept.ID, Name = dept.Name, IsSelected = true };
                    SelectedCategories.Add(item);
                }
            }

            // Todas las categorías disponibles
            foreach (var dept in allDepts)
            {
                var item = new SelectableCategoryItem
                {
                    ID = dept.ID,
                    Name = dept.Name,
                    IsSelected = selectedIds.Contains(dept.ID)
                };
                AllCategories.Add(item);
            }

            RefreshCounters();
        }
        catch (Exception)
        {
            await _dialogService.AlertAsync("Error", "No se pudieron cargar las categorías.", "OK");
        }
        finally
        {
            IsBusy = false;
        }
    }

    private void ToggleCategory(SelectableCategoryItem? item)
    {
        if (item is null) return;

        if (item.IsSelected)
        {
            // Deseleccionar
            item.IsSelected = false;
            var toRemove = SelectedCategories.FirstOrDefault(c => c.ID == item.ID);
            if (toRemove is not null)
                SelectedCategories.Remove(toRemove);
        }
        else
        {
            if (SelectedCategories.Count >= MaxCategories)
                return;

            // Seleccionar
            item.IsSelected = true;
            SelectedCategories.Add(new SelectableCategoryItem { ID = item.ID, Name = item.Name, IsSelected = true });
        }

        RefreshCounters();
    }

    private void RemoveCategory(SelectableCategoryItem? item)
    {
        if (item is null) return;

        SelectedCategories.Remove(item);

        var source = AllCategories.FirstOrDefault(c => c.ID == item.ID);
        if (source is not null)
            source.IsSelected = false;

        RefreshCounters();
    }

    private void MoveUp(SelectableCategoryItem? item)
    {
        if (item is null) return;

        var idx = SelectedCategories.IndexOf(item);
        if (idx > 0)
            SelectedCategories.Move(idx, idx - 1);
    }

    private void MoveDown(SelectableCategoryItem? item)
    {
        if (item is null) return;

        var idx = SelectedCategories.IndexOf(item);
        if (idx >= 0 && idx < SelectedCategories.Count - 1)
            SelectedCategories.Move(idx, idx + 1);
    }

    private async Task SaveAsync()
    {
        if (IsSaving) return;
        IsSaving = true;

        try
        {
            var ids = string.Join(",", SelectedCategories.Select(c => c.ID));
            var success = await _configService.SaveCategoryConfigAsync(ids);

            if (success)
            {
                await _mainVm.ReloadCategoriesAsync();
                await Shell.Current.GoToAsync("..");
            }
            else
            {
                await _dialogService.AlertAsync("Error", "No se pudo guardar la configuración.", "OK");
            }
        }
        catch (Exception)
        {
            await _dialogService.AlertAsync("Error", "Ocurrió un error al guardar.", "OK");
        }
        finally
        {
            IsSaving = false;
        }
    }

    private void RefreshCounters()
    {
        OnPropertyChanged(nameof(SelectedCount));
        OnPropertyChanged(nameof(SelectedCountText));
        OnPropertyChanged(nameof(CanAddMore));
    }

    public event PropertyChangedEventHandler? PropertyChanged;
    protected void OnPropertyChanged([CallerMemberName] string? name = null)
        => PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(name));
}

public class SelectableCategoryItem : INotifyPropertyChanged
{
    private bool _isSelected;

    public int ID { get; set; }
    public string Name { get; set; } = string.Empty;

    public bool IsSelected
    {
        get => _isSelected;
        set { if (_isSelected != value) { _isSelected = value; OnPropertyChanged(); } }
    }

    public event PropertyChangedEventHandler? PropertyChanged;
    protected void OnPropertyChanged([CallerMemberName] string? name = null)
        => PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(name));
}
