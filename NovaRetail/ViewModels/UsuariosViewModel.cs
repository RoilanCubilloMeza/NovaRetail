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
    private readonly IUsuariosService _service;
    private readonly IDialogService _dialog;

    private bool _isBusy;
    private bool _isSaving;
    private string _statusMessage = string.Empty;
    private bool _isEditing;

    // Campos del formulario
    private int _editingId;
    private string _editNombreUsuario = string.Empty;
    private string _editNombreCompleto = string.Empty;
    private short _editSecurityLevel;
    private RolModel? _editSelectedRol;

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

    public bool IsEditing
    {
        get => _isEditing;
        private set { if (_isEditing != value) { _isEditing = value; OnPropertyChanged(); OnPropertyChanged(nameof(FormTitle)); } }
    }

    public string FormTitle => IsEditing ? $"Editar Usuario #{_editingId}" : "Seleccione un usuario";

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
        set { if (_editSecurityLevel != value) { _editSecurityLevel = value; OnPropertyChanged(); } }
    }

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

    public UsuariosViewModel(IUsuariosService service, IDialogService dialog)
    {
        _service = service;
        _dialog = dialog;
        Usuarios.CollectionChanged += (_, _) => OnPropertyChanged(nameof(HasUsuarios));

        SaveCommand = new Command(async () => await SaveAsync());
        EditCommand = new Command<UsuarioEditItem>(LoadForEdit);
        ToggleActivoCommand = new Command<UsuarioEditItem>(async item => await ToggleActivoAsync(item));
        CancelCommand = new Command(ClearForm);
        GoBackCommand = new Command(async () => await Shell.Current.GoToAsync(".."));
    }

    public async Task LoadAsync()
    {
        if (IsBusy) return;
        IsBusy = true;
        StatusMessage = string.Empty;

        try
        {
            var usuariosTask = _service.GetUsuariosAsync();
            var rolesTask = _service.GetRolesAsync();

            await Task.WhenAll(usuariosTask, rolesTask);

            var usuarios = await usuariosTask;
            var roles = await rolesTask;

            await MainThread.InvokeOnMainThreadAsync(() =>
            {
                RolesDisponibles.Clear();
                foreach (var r in roles)
                    RolesDisponibles.Add(r);

                Usuarios.Clear();
                foreach (var u in usuarios)
                {
                    Usuarios.Add(new UsuarioEditItem
                    {
                        Id = u.Id,
                        NombreUsuario = u.NombreUsuario,
                        NombreCompleto = u.NombreCompleto,
                        SecurityLevel = u.SecurityLevel,
                        Privileges = u.Privileges,
                        StoreID = u.StoreID,
                        RoleId = u.RoleId,
                        RolCode = u.RolCode,
                        RolName = u.RolName
                    });
                }
            });

            StatusMessage = $"{usuarios.Count} usuario(s) cargado(s).";
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

    // Guarda referencia al item original para detectar cambios
    private UsuarioEditItem? _originalItem;

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
                StatusMessage = $"✅ Usuario '{nombre}' actualizado.";
                await _dialog.AlertAsync("✅ Usuario Actualizado",
                    $"Se actualizó el usuario '{nombre}'.\n\n{cambios}", "Aceptar");
            }
            else
            {
                StatusMessage = "❌ Error al guardar el usuario.";
                await _dialog.AlertAsync("❌ Error", "No se pudo guardar el usuario.", "Aceptar");
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
            parts.Add($"Nombre → '{EditNombreCompleto.Trim()}'");
        if (_originalItem.SecurityLevel != EditSecurityLevel)
            parts.Add($"Nivel de seguridad → {EditSecurityLevel}");
        var newRoleId = EditSelectedRol?.Id ?? 0;
        if (_originalItem.RoleId != newRoleId)
            parts.Add($"Rol → '{EditSelectedRol?.Name ?? "Sin rol"}'");

        return parts.Count > 0 ? "Cambios: " + string.Join(", ", parts) : "Sin cambios detectados.";
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
                await LoadAsync();
                StatusMessage = $"✅ Usuario '{item.NombreUsuario}' {accionPasada}.";
                await _dialog.AlertAsync($"✅ Usuario {accionPasada}",
                    $"El usuario '{item.NombreUsuario}' ({item.NombreCompleto}) fue {accionPasada}.\nNivel de seguridad → {nuevoNivel}", "Aceptar");
            }
            else
            {
                StatusMessage = $"❌ Error al {accion} '{item.NombreUsuario}'.";
                await _dialog.AlertAsync("❌ Error", $"No se pudo {accion} el usuario '{item.NombreUsuario}'.", "Aceptar");
            }
        }
        catch (Exception ex)
        {
            StatusMessage = $"❌ Error: {ex.Message}";
            await _dialog.AlertAsync("❌ Error", ex.Message, "Aceptar");
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
    public Color EstadoColor => IsActivo ? Colors.Green : Colors.Gray;
    public string ToggleTexto => IsActivo ? "🚫 Desactivar" : "✅ Activar";
    public Color ToggleBgColor => IsActivo ? Color.FromArgb("#FEF2F2") : Color.FromArgb("#F0FDF4");
    public Color ToggleTextColor => IsActivo ? Color.FromArgb("#DC2626") : Color.FromArgb("#16A34A");

    public event PropertyChangedEventHandler? PropertyChanged;
    private void OnPropertyChanged([CallerMemberName] string? name = null)
        => PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(name));
}
