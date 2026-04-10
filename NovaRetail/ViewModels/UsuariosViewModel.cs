using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Runtime.CompilerServices;
using System.Windows.Input;
using NovaRetail.Data;
using NovaRetail.Models;
using NovaRetail.Services;

namespace NovaRetail.ViewModels;

public class UsuariosViewModel : INotifyPropertyChanged
{
    private const string EstadoTodos = "todos";
    private const string EstadoActivos = "activos";
    private const string EstadoInactivos = "inactivos";

    private readonly IUsuariosService _service;
    private readonly IDialogService _dialog;

    private bool _isBusy;
    private bool _isSaving;
    private string _statusMessage = string.Empty;
    private bool _isEditing;
    private string _searchText = string.Empty;
    private string _estadoFiltro = EstadoTodos;

    private int _editingId;
    private string _editNombreUsuario = string.Empty;
    private string _editNombreCompleto = string.Empty;
    private short _editSecurityLevel;
    private RolModel? _editSelectedRol;
    private UsuarioEditItem? _originalItem;

    public ObservableCollection<UsuarioEditItem> Usuarios { get; } = new();
    public ObservableCollection<RolModel> RolesDisponibles { get; } = new();

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
    public bool HasUsuarios => Usuarios.Count > 0;
    public bool HasSearchText => !string.IsNullOrWhiteSpace(SearchText);
    public bool IsBrowseMode => !IsEditing;

    public bool IsEditing
    {
        get => _isEditing;
        private set
        {
            if (_isEditing != value)
            {
                _isEditing = value;
                OnPropertyChanged();
                OnPropertyChanged(nameof(IsBrowseMode));
                OnPropertyChanged(nameof(FormTitle));
            }
        }
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
                OnPropertyChanged(nameof(HasSearchText));
                OnPropertyChanged(nameof(ResultadosResumen));
                OnPropertyChanged(nameof(EmptyMessage));
            }
        }
    }

    public bool FiltroTodosActivo => string.Equals(_estadoFiltro, EstadoTodos, StringComparison.OrdinalIgnoreCase);
    public bool FiltroActivosActivo => string.Equals(_estadoFiltro, EstadoActivos, StringComparison.OrdinalIgnoreCase);
    public bool FiltroInactivosActivo => string.Equals(_estadoFiltro, EstadoInactivos, StringComparison.OrdinalIgnoreCase);

    public string FormTitle => IsEditing ? $"Editar Usuario #{_editingId}" : string.Empty;

    public string ResultadosResumen
    {
        get
        {
            if (HasUsuarios)
                return $"{Usuarios.Count} usuario(s) encontrados";

            return HasSearchText || !FiltroTodosActivo
                ? "No hay resultados para ese filtro."
                : "No hay usuarios disponibles.";
        }
    }

    public string EmptyMessage => HasSearchText || !FiltroTodosActivo
        ? "No se encontraron usuarios con los criterios indicados."
        : "No se encontraron usuarios.";

    public string EditNombreUsuario
    {
        get => _editNombreUsuario;
        set { if (_editNombreUsuario != value) { _editNombreUsuario = value; OnPropertyChanged(); } }
    }

    public string EditNombreCompleto
    {
        get => _editNombreCompleto;
        set { if (_editNombreCompleto != value) { _editNombreCompleto = value; OnPropertyChanged(); } }
    }

    public short EditSecurityLevel
    {
        get => _editSecurityLevel;
        set
        {
            if (_editSecurityLevel != value)
            {
                _editSecurityLevel = value;
                OnPropertyChanged();
                OnPropertyChanged(nameof(EditEstadoTexto));
                OnPropertyChanged(nameof(EditEstadoDescripcion));
                OnPropertyChanged(nameof(EditEstadoColor));
                OnPropertyChanged(nameof(EditEstadoBackground));
            }
        }
    }

    public string EditEstadoTexto => EditSecurityLevel > 0 ? "Activo" : "Inactivo";

    public string EditEstadoDescripcion => EditSecurityLevel > 0
        ? "El usuario puede iniciar sesión en el sistema."
        : "El usuario está desactivado para ingresar.";

    public Color EditEstadoColor => EditSecurityLevel > 0 ? Color.FromArgb("#166534") : Color.FromArgb("#64748B");

    public Color EditEstadoBackground => EditSecurityLevel > 0 ? Color.FromArgb("#DCFCE7") : Color.FromArgb("#E2E8F0");

    public RolModel? EditSelectedRol
    {
        get => _editSelectedRol;
        set { if (_editSelectedRol != value) { _editSelectedRol = value; OnPropertyChanged(); } }
    }

    public ICommand SaveCommand { get; }
    public ICommand EditCommand { get; }
    public ICommand ToggleActivoCommand { get; }
    public ICommand CancelCommand { get; }
    public ICommand GoBackCommand { get; }
    public ICommand SearchCommand { get; }
    public ICommand ClearSearchCommand { get; }
    public ICommand ShowTodosCommand { get; }
    public ICommand ShowActivosCommand { get; }
    public ICommand ShowInactivosCommand { get; }

    public UsuariosViewModel(IUsuariosService service, IDialogService dialog)
    {
        _service = service;
        _dialog = dialog;
        Usuarios.CollectionChanged += (_, _) =>
        {
            OnPropertyChanged(nameof(HasUsuarios));
            OnPropertyChanged(nameof(ResultadosResumen));
            OnPropertyChanged(nameof(EmptyMessage));
        };

        SaveCommand = new Command(async () => await SaveAsync());
        EditCommand = new Command<UsuarioEditItem>(LoadForEdit);
        ToggleActivoCommand = new Command<UsuarioEditItem>(async item => await ToggleActivoAsync(item));
        CancelCommand = new Command(ClearForm);
        GoBackCommand = new Command(async () => await Shell.Current.GoToAsync(".."));
        SearchCommand = new Command(async () => await SearchAsync());
        ClearSearchCommand = new Command(async () => await ClearSearchAsync());
        ShowTodosCommand = new Command(async () => await SetEstadoFiltroAsync(EstadoTodos));
        ShowActivosCommand = new Command(async () => await SetEstadoFiltroAsync(EstadoActivos));
        ShowInactivosCommand = new Command(async () => await SetEstadoFiltroAsync(EstadoInactivos));
    }

    public async Task LoadAsync()
    {
        if (IsBusy) return;
        IsBusy = true;
        StatusMessage = string.Empty;

        try
        {
            var busqueda = string.IsNullOrWhiteSpace(SearchText) ? null : SearchText.Trim();
            var estado = GetEstadoApiValue();

            var usuariosTask = _service.GetUsuariosAsync(busqueda, estado);
            var rolesTask = _service.GetRolesAsync();

            await Task.WhenAll(usuariosTask, rolesTask);

            var usuarios = await usuariosTask;
            var roles = await rolesTask;

            await MainThread.InvokeOnMainThreadAsync(() =>
            {
                RolesDisponibles.Clear();
                foreach (var rol in roles)
                    RolesDisponibles.Add(rol);

                Usuarios.Clear();
                foreach (var usuario in usuarios)
                {
                    Usuarios.Add(new UsuarioEditItem
                    {
                        Id = usuario.Id,
                        NombreUsuario = usuario.NombreUsuario,
                        NombreCompleto = usuario.NombreCompleto,
                        SecurityLevel = usuario.SecurityLevel,
                        Privileges = usuario.Privileges,
                        StoreID = usuario.StoreID,
                        RoleId = usuario.RoleId,
                        RolCode = usuario.RolCode,
                        RolName = usuario.RolName
                    });
                }
            });

            StatusMessage = BuildLoadMessage(usuarios.Count, busqueda, estado);
            OnPropertyChanged(nameof(ResultadosResumen));
            OnPropertyChanged(nameof(EmptyMessage));
        }
        catch (Exception ex)
        {
            StatusMessage = $"Error al cargar usuarios: {ex.Message}";
        }
        finally
        {
            IsBusy = false;
        }
    }

    private async Task SearchAsync()
    {
        ClearForm();
        await LoadAsync();
    }

    private async Task ClearSearchAsync()
    {
        SearchText = string.Empty;
        ClearForm();
        await LoadAsync();
    }

    private async Task SetEstadoFiltroAsync(string estado)
    {
        if (string.Equals(_estadoFiltro, estado, StringComparison.OrdinalIgnoreCase))
            return;

        _estadoFiltro = estado;
        NotifyFilterStateChanged();
        ClearForm();
        await LoadAsync();
    }

    private void NotifyFilterStateChanged()
    {
        OnPropertyChanged(nameof(FiltroTodosActivo));
        OnPropertyChanged(nameof(FiltroActivosActivo));
        OnPropertyChanged(nameof(FiltroInactivosActivo));
        OnPropertyChanged(nameof(ResultadosResumen));
        OnPropertyChanged(nameof(EmptyMessage));
    }

    private string? GetEstadoApiValue()
    {
        if (FiltroActivosActivo) return EstadoActivos;
        if (FiltroInactivosActivo) return EstadoInactivos;
        return null;
    }

    private static string BuildLoadMessage(int count, string? busqueda, string? estado)
    {
        var filtros = new List<string>();

        if (!string.IsNullOrWhiteSpace(busqueda))
            filtros.Add($"búsqueda '{busqueda}'");

        if (string.Equals(estado, EstadoActivos, StringComparison.OrdinalIgnoreCase))
            filtros.Add("solo activos");
        else if (string.Equals(estado, EstadoInactivos, StringComparison.OrdinalIgnoreCase))
            filtros.Add("solo inactivos");

        if (filtros.Count == 0)
            return $"{count} usuario(s) cargado(s).";

        return $"{count} usuario(s) para {string.Join(" y ", filtros)}.";
    }

    private void LoadForEdit(UsuarioEditItem? item)
    {
        if (item is null) return;

        _originalItem = item;
        _editingId = item.Id;
        EditNombreUsuario = item.NombreUsuario;
        EditNombreCompleto = item.NombreCompleto;
        EditSecurityLevel = item.SecurityLevel;
        EditSelectedRol = RolesDisponibles.FirstOrDefault(r => r.Id == item.RoleId);
        IsEditing = true;
        StatusMessage = string.Empty;
    }

    private void ClearForm()
    {
        _editingId = 0;
        _originalItem = null;
        EditNombreUsuario = string.Empty;
        EditNombreCompleto = string.Empty;
        EditSecurityLevel = 0;
        EditSelectedRol = null;
        IsEditing = false;
    }

    private async Task SaveAsync()
    {
        if (_editingId <= 0)
        {
            StatusMessage = "Seleccione un usuario para editar.";
            return;
        }

        IsSaving = true;
        StatusMessage = string.Empty;

        try
        {
            var roleId = EditSelectedRol?.Id ?? 0;
            var ok = await _service.SaveUsuarioAsync(_editingId, EditNombreCompleto.Trim(), EditSecurityLevel, roleId);
            if (ok)
            {
                var cambios = BuildCambiosMessage();
                var nombre = EditNombreUsuario;
                ClearForm();
                await LoadAsync();
                StatusMessage = $"Usuario '{nombre}' actualizado.";
                await _dialog.AlertAsync("Usuario Actualizado",
                    $"El usuario {nombre} se actualizó correctamente.\n\n{cambios}", "Aceptar");
            }
            else
            {
                StatusMessage = "Error al guardar el usuario.";
                await _dialog.AlertAsync("Error", "No se pudo guardar el usuario. Intente de nuevo.", "Aceptar");
            }
        }
        catch (Exception ex)
        {
            StatusMessage = $"Error al guardar: {ex.Message}";
        }
        finally
        {
            IsSaving = false;
        }
    }

    private string BuildCambiosMessage()
    {
        if (_originalItem is null) return string.Empty;

        var parts = new List<string>();
        if (!string.Equals(_originalItem.NombreCompleto, EditNombreCompleto.Trim(), StringComparison.Ordinal))
            parts.Add($"• Nombre: {EditNombreCompleto.Trim()}");
        if (_originalItem.SecurityLevel != EditSecurityLevel)
            parts.Add($"• Nivel de seguridad: {EditSecurityLevel}");
        var newRoleId = EditSelectedRol?.Id ?? 0;
        if (_originalItem.RoleId != newRoleId)
            parts.Add($"• Rol: {EditSelectedRol?.Name ?? "Sin rol"}");

        return parts.Count > 0 ? "Cambios realizados:\n" + string.Join("\n", parts) : "No se detectaron cambios.";
    }

    private async Task ToggleActivoAsync(UsuarioEditItem? item)
    {
        if (item is null) return;

        var accion = item.IsActivo ? "desactivar" : "reactivar";
        var confirmar = await _dialog.ConfirmAsync(
            $"¿Seguro que desea {accion}?",
            $"Se va a {accion} el usuario '{item.NombreUsuario}' ({item.NombreCompleto}).",
            "Sí", "No");
        if (!confirmar) return;

        IsSaving = true;
        StatusMessage = string.Empty;

        try
        {
            var nuevoNivel = item.IsActivo ? (short)0 : (short)1;
            var accionPasada = item.IsActivo ? "desactivado" : "reactivado";

            var ok = await _service.SaveUsuarioAsync(item.Id, item.NombreCompleto, nuevoNivel, item.RoleId);
            if (ok)
            {
                if (item.Id == _editingId)
                    ClearForm();

                await LoadAsync();
                StatusMessage = $"Usuario '{item.NombreUsuario}' {accionPasada}.";
                await _dialog.AlertAsync($"Usuario {accionPasada}",
                    $"El usuario {item.NombreUsuario} ({item.NombreCompleto}) fue {accionPasada} correctamente.", "Aceptar");
            }
            else
            {
                StatusMessage = $"Error al {accion} '{item.NombreUsuario}'.";
                await _dialog.AlertAsync("Error", $"No se pudo {accion} el usuario {item.NombreUsuario}. Intente de nuevo.", "Aceptar");
            }
        }
        catch (Exception ex)
        {
            StatusMessage = $"Error: {ex.Message}";
            await _dialog.AlertAsync("Error", ex.Message, "Aceptar");
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

public class UsuarioEditItem : INotifyPropertyChanged
{
    public int Id { get; set; }
    public string NombreUsuario { get; set; } = string.Empty;
    public string NombreCompleto { get; set; } = string.Empty;
    public short SecurityLevel { get; set; }
    public int Privileges { get; set; }
    public int StoreID { get; set; }
    public int RoleId { get; set; }
    public string RolCode { get; set; } = string.Empty;
    public string RolName { get; set; } = string.Empty;

    public string RolDisplay => string.IsNullOrWhiteSpace(RolName) ? "Sin rol" : RolName;
    public string SecurityDisplay => $"Nivel {SecurityLevel}";
    public bool IsActivo => SecurityLevel > 0;
    public string EstadoTexto => IsActivo ? "Activo" : "Inactivo";
    public Color EstadoColor => IsActivo ? Color.FromArgb("#166534") : Color.FromArgb("#64748B");
    public Color EstadoBackgroundColor => IsActivo ? Color.FromArgb("#DCFCE7") : Color.FromArgb("#E2E8F0");
    public string ToggleTexto => IsActivo ? "Desactivar" : "Activar";
    public Color ToggleBgColor => IsActivo ? Color.FromArgb("#FEF2F2") : Color.FromArgb("#F0FDF4");
    public Color ToggleTextColor => IsActivo ? Color.FromArgb("#DC2626") : Color.FromArgb("#16A34A");

    public event PropertyChangedEventHandler? PropertyChanged;
    private void OnPropertyChanged([CallerMemberName] string? name = null)
        => PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(name));
}
