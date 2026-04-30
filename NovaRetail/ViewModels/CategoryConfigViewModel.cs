using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Runtime.CompilerServices;
using System.Windows.Input;
using NovaRetail.Data;
using NovaRetail.Models;
using NovaRetail.Services;
using NovaRetail.State;

namespace NovaRetail.ViewModels;

public class CategoryConfigViewModel : INotifyPropertyChanged
{
    private const int MaxCategories = 6;
    private const string ProductViewList = "List";
    private const string ProductViewCards = "Cards";

    private readonly IStoreConfigService _configService;
    private readonly IDialogService _dialogService;
    private readonly MainViewModel _mainVm;
    private readonly UserSession _userSession;

    private bool _isBusy;
    private bool _isSaving;
    private PersonalizationSection _activeSection = PersonalizationSection.Categories;
    private string _productViewMode = ProductViewList;

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

    public bool IsCategoriesSectionActive => _activeSection == PersonalizationSection.Categories;
    public bool IsProductViewSectionActive => _activeSection == PersonalizationSection.ProductView;
    public bool IsCategoriesSectionVisible => IsCategoriesSectionActive;
    public bool IsProductViewSectionVisible => IsProductViewSectionActive;

    public string ProductViewMode
    {
        get => _productViewMode;
        private set
        {
            var normalized = NormalizeProductViewMode(value);
            if (_productViewMode != normalized)
            {
                _productViewMode = normalized;
                OnPropertyChanged();
                OnPropertyChanged(nameof(IsProductListView));
                OnPropertyChanged(nameof(IsProductCardView));
                OnPropertyChanged(nameof(ProductViewModeTitle));
                OnPropertyChanged(nameof(ProductViewModeDescription));
            }
        }
    }

    public bool IsProductListView => ProductViewMode == ProductViewList;
    public bool IsProductCardView => ProductViewMode == ProductViewCards;
    public string ProductViewModeTitle => IsProductCardView ? "Vista carta" : "Vista lista";
    public string ProductViewModeDescription => IsProductCardView
        ? "Tarjetas visuales para navegar productos con mas espacio."
        : "Lista compacta y rapida para cobrar con menos desplazamiento.";

    public ICommand ToggleCategoryCommand { get; }
    public ICommand MoveUpCommand { get; }
    public ICommand MoveDownCommand { get; }
    public ICommand RemoveCategoryCommand { get; }
    public ICommand SaveCommand { get; }
    public ICommand GoBackCommand { get; }
    public ICommand ShowCategoriesSectionCommand { get; }
    public ICommand ShowProductViewSectionCommand { get; }
    public ICommand SelectProductViewModeCommand { get; }

    public CategoryConfigViewModel(IStoreConfigService configService, IDialogService dialogService, MainViewModel mainVm, UserSession userSession)
    {
        _configService = configService;
        _dialogService = dialogService;
        _mainVm = mainVm;
        _userSession = userSession;

        ToggleCategoryCommand = new Command<SelectableCategoryItem>(ToggleCategory);
        MoveUpCommand = new Command<SelectableCategoryItem>(MoveUp);
        MoveDownCommand = new Command<SelectableCategoryItem>(MoveDown);
        RemoveCategoryCommand = new Command<SelectableCategoryItem>(RemoveCategory);
        SaveCommand = new Command(async () => await SaveAsync());
        GoBackCommand = new Command(async () => await Shell.Current.GoToAsync(".."));
        ShowCategoriesSectionCommand = new Command(() => SetActiveSection(PersonalizationSection.Categories));
        ShowProductViewSectionCommand = new Command(() => SetActiveSection(PersonalizationSection.ProductView));
        SelectProductViewModeCommand = new Command<string>(SetProductViewMode);
    }

    public async Task LoadAsync()
    {
        if (IsBusy) return;
        IsBusy = true;

        try
        {
            var userName = _userSession.CurrentUser?.UserName;
            var allTask = _configService.GetAllCategoriesAsync();
            var configTask = _configService.GetCategoryConfigAsync(userName);
            var productViewTask = _configService.GetProductViewModeAsync(userName);
            await Task.WhenAll(allTask, configTask, productViewTask);

            var allDepts = allTask.Result;
            var currentConfig = configTask.Result;
            ProductViewMode = string.IsNullOrWhiteSpace(productViewTask.Result) ? ProductViewList : productViewTask.Result;

            var selectedIds = new List<int>();
            if (!string.IsNullOrWhiteSpace(currentConfig))
            {
                foreach (var s in currentConfig.Split(',', StringSplitOptions.RemoveEmptyEntries))
                {
                    if (int.TryParse(s.Trim(), out var id) && !selectedIds.Contains(id))
                        selectedIds.Add(id);
                }
            }

            AllCategories.Clear();
            SelectedCategories.Clear();

            var deptMap = new Dictionary<int, CategoryModel>();
            foreach (var d in allDepts)
            {
                if (!deptMap.ContainsKey(d.ID))
                    deptMap[d.ID] = d;
            }

            foreach (var id in selectedIds)
            {
                if (deptMap.TryGetValue(id, out var dept))
                    SelectedCategories.Add(new SelectableCategoryItem { ID = dept.ID, Name = dept.Name, IsSelected = true });
            }

            foreach (var dept in allDepts)
            {
                AllCategories.Add(new SelectableCategoryItem
                {
                    ID = dept.ID,
                    Name = dept.Name,
                    IsSelected = selectedIds.Contains(dept.ID)
                });
            }

            RefreshCounters();
        }
        catch (Exception)
        {
            await _dialogService.AlertAsync("Error", "No se pudo cargar la personalizacion.", "OK");
        }
        finally
        {
            IsBusy = false;
        }
    }

    private void SetActiveSection(PersonalizationSection section)
    {
        if (_activeSection == section)
            return;

        _activeSection = section;
        OnPropertyChanged(nameof(IsCategoriesSectionActive));
        OnPropertyChanged(nameof(IsProductViewSectionActive));
        OnPropertyChanged(nameof(IsCategoriesSectionVisible));
        OnPropertyChanged(nameof(IsProductViewSectionVisible));
    }

    private void SetProductViewMode(string? mode)
    {
        ProductViewMode = NormalizeProductViewMode(mode);
    }

    private void ToggleCategory(SelectableCategoryItem? item)
    {
        if (item is null) return;

        if (item.IsSelected)
        {
            item.IsSelected = false;
            var toRemove = SelectedCategories.FirstOrDefault(c => c.ID == item.ID);
            if (toRemove is not null)
                SelectedCategories.Remove(toRemove);
        }
        else
        {
            if (SelectedCategories.Count >= MaxCategories)
                return;

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
            var userName = _userSession.CurrentUser?.UserName;

            var categoryTask = _configService.SaveCategoryConfigAsync(ids, userName);
            var viewModeTask = _configService.SaveProductViewModeAsync(ProductViewMode, userName);
            await Task.WhenAll(categoryTask, viewModeTask);

            if (categoryTask.Result && viewModeTask.Result)
            {
                await _mainVm.ReloadCategoriesAsync();
                await _mainVm.ProductCatalog.RefreshProductViewModeAsync();
                await Shell.Current.GoToAsync("..");
            }
            else
            {
                await _dialogService.AlertAsync("Error", "No se pudo guardar la personalizacion.", "OK");
            }
        }
        catch (Exception)
        {
            await _dialogService.AlertAsync("Error", "Ocurrio un error al guardar.", "OK");
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

    private static string NormalizeProductViewMode(string? mode)
        => string.Equals(mode, ProductViewCards, StringComparison.OrdinalIgnoreCase)
            ? ProductViewCards
            : ProductViewList;

    public event PropertyChangedEventHandler? PropertyChanged;
    protected void OnPropertyChanged([CallerMemberName] string? name = null)
        => PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(name));

    private enum PersonalizationSection
    {
        Categories,
        ProductView
    }
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
