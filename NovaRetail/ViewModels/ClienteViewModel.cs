using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Globalization;
using System.Runtime.CompilerServices;
using System.Text;
using System.Text.Json.Nodes;
using System.Text.RegularExpressions;
using System.Windows.Input;
using NovaRetail.Data;
using NovaRetail.Models;
using NovaRetail.Services;
using NovaRetail.State;

namespace NovaRetail.ViewModels
{
    public enum SyncStatus { Idle, Syncing, Synced, NotFound }
    public enum FormMessageKind { None, Info, Warning, Error }

    public class ClienteViewModel : INotifyPropertyChanged
    {
        private readonly IClienteService _clienteService;
        private readonly IDialogService _dialogService;
        private readonly AppStore _appStore;

        private string _clientId = string.Empty;
        private string _idType = "Cédula Física";
        private string _name = string.Empty;
        private bool _isReceiver;
        private string _phone = string.Empty;
        private string _email = string.Empty;
        private string _email2 = string.Empty;
        private string? _selectedProvince;
        private string? _selectedCanton;
        private string? _selectedDistrict;
        private string? _selectedBarrio;
        private string _selectedCustomerType = string.Empty;
        private string _locationSearch = string.Empty;
        private string _address = string.Empty;
        private string _activityCode = string.Empty;
        private string _activityDescription = string.Empty;
        private SyncStatus _syncStatus = SyncStatus.Idle;
        private string _validationMessage = string.Empty;
        private FormMessageKind _validationMessageKind = FormMessageKind.None;
        private string _lastSyncedClientId = string.Empty;
        private string _lastAutoSyncedClientId = string.Empty;
        private bool _isAutoSyncInProgress;
        private bool _isExistingCustomer;
        private bool _existingCustomerUpdateConfirmed;
        private ClienteModel? _pendingLocationModel;


        public string ClientId
        {
            get => _clientId;
            set
            {
                var sanitized = SanitizeClientIdByType(value, IdType);
                if (_clientId == sanitized) return;
                _clientId = sanitized;
                OnPropertyChanged();
                OnPropertyChanged(nameof(ClientStatusTitle));
                OnPropertyChanged(nameof(ClientStatusHint));
                OnPropertyChanged(nameof(ClientStatusBackgroundColor));
                OnPropertyChanged(nameof(ClientStatusBorderColor));
                OnPropertyChanged(nameof(ClientStatusTitleColor));

                if (!string.Equals(_clientId, _lastSyncedClientId, StringComparison.Ordinal))
                {
                    IsExistingCustomer = false;
                    _existingCustomerUpdateConfirmed = false;
                }

                if (ShouldAutoSyncClientId(_clientId))
                    _ = TryAutoSyncAsync(_clientId);
            }
        }

        public string IdType
        {
            get => _idType;
            set
            {
                _idType = value;
                OnPropertyChanged();
                OnPropertyChanged(nameof(IsPhysicalId));
                OnPropertyChanged(nameof(IsLegalId));
                OnPropertyChanged(nameof(IsForeignId));
                OnPropertyChanged(nameof(ClientIdKeyboard));
                ClientId = _clientId;
            }
        }

        public bool IsExistingCustomer
        {
            get => _isExistingCustomer;
            private set
            {
                if (_isExistingCustomer == value) return;
                _isExistingCustomer = value;
                OnPropertyChanged();
                OnPropertyChanged(nameof(ClientStatusTitle));
                OnPropertyChanged(nameof(ClientStatusHint));
                OnPropertyChanged(nameof(ClientStatusBackgroundColor));
                OnPropertyChanged(nameof(ClientStatusBorderColor));
                OnPropertyChanged(nameof(ClientStatusTitleColor));
                OnPropertyChanged(nameof(SaveButtonText));
                OnPropertyChanged(nameof(SaveAndReturnButtonText));
                OnPropertyChanged(nameof(SaveActionTitle));
                OnPropertyChanged(nameof(SaveActionHint));
            }
        }

        public string Name
        {
            get => _name;
            set { _name = value; OnPropertyChanged(); }
        }

        public bool IsReceiver
        {
            get => _isReceiver;
            set
            {
                _isReceiver = value;
                OnPropertyChanged();
                OnPropertyChanged(nameof(ShowTaxData));
                OnPropertyChanged(nameof(IsNotReceiver));
                OnPropertyChanged(nameof(TaxSectionTitle));
                OnPropertyChanged(nameof(TaxSectionHint));
            }
        }

        public string Phone
        {
            get => _phone;
            set { _phone = value; OnPropertyChanged(); }
        }

        public string Email
        {
            get => _email;
            set { _email = value; OnPropertyChanged(); }
        }

        public string Email2
        {
            get => _email2;
            set { _email2 = value; OnPropertyChanged(); }
        }

        public string? SelectedProvince
        {
            get => _selectedProvince;
            set
            {
                _selectedProvince = value;
                OnPropertyChanged();
                LoadCantons(value);
                SelectedCanton = null;
                OnPropertyChanged(nameof(CanHaveCanton));
                OnPropertyChanged(nameof(CantonOpacity));
            }
        }

        public string? SelectedCanton
        {
            get => _selectedCanton;
            set
            {
                _selectedCanton = value;
                OnPropertyChanged();
                LoadDistricts(value);
                SelectedDistrict = null;
                OnPropertyChanged(nameof(CanHaveDistrict));
                OnPropertyChanged(nameof(DistrictOpacity));
            }
        }

        public string? SelectedDistrict
        {
            get => _selectedDistrict;
            set
            {
                _selectedDistrict = value;
                OnPropertyChanged();
                LoadBarrios(SelectedProvince, SelectedCanton, value);
                SelectedBarrio = null;
                OnPropertyChanged(nameof(CanHaveBarrio));
                OnPropertyChanged(nameof(BarrioOpacity));
            }
        }

        public string? SelectedBarrio
        {
            get => _selectedBarrio;
            set { _selectedBarrio = value; OnPropertyChanged(); }
        }

        public string SelectedCustomerType
        {
            get => _selectedCustomerType;
            set { _selectedCustomerType = value; OnPropertyChanged(); }
        }

        public string LocationSearch
        {
            get => _locationSearch;
            set
            {
                _locationSearch = value;
                OnPropertyChanged();
                ApplyLocationSearch(value);
            }
        }

        public string Address
        {
            get => _address;
            set { _address = value; OnPropertyChanged(); }
        }

        public string ActivityCode
        {
            get => _activityCode;
            set { _activityCode = value; OnPropertyChanged(); }
        }

        public string ActivityDescription
        {
            get => _activityDescription;
            set
            {
                _activityDescription = value;
                OnPropertyChanged();
                OnPropertyChanged(nameof(HasActivityDescription));
            }
        }

        public SyncStatus SyncStatus
        {
            get => _syncStatus;
            set
            {
                _syncStatus = value;
                OnPropertyChanged();
                OnPropertyChanged(nameof(SyncText));
                OnPropertyChanged(nameof(IsSyncing));
                OnPropertyChanged(nameof(IsNotSyncing));
                OnPropertyChanged(nameof(SyncButtonBackground));
                OnPropertyChanged(nameof(SyncButtonTextColor));
                OnPropertyChanged(nameof(ClientStatusTitle));
                OnPropertyChanged(nameof(ClientStatusHint));
                OnPropertyChanged(nameof(ClientStatusBackgroundColor));
                OnPropertyChanged(nameof(ClientStatusBorderColor));
                OnPropertyChanged(nameof(ClientStatusTitleColor));
            }
        }

        public string ValidationMessage
        {
            get => _validationMessage;
            set
            {
                _validationMessage = value;
                OnPropertyChanged();
                OnPropertyChanged(nameof(HasValidationMessage));
            }
        }

        // ──────── Computed (bool) ────────

        public bool ShowTaxData          => _isReceiver;
        public bool IsNotReceiver        => !_isReceiver;
        public bool IsPhysicalId         => _idType == "Cédula Física";
        public bool IsLegalId            => _idType == "Cédula Jurídica";
        public bool IsForeignId          => _idType == "Extranjero No Domiciliado";
        public bool IsSyncing            => _syncStatus == SyncStatus.Syncing;
        public bool IsNotSyncing         => _syncStatus != SyncStatus.Syncing;
        public bool HasValidationMessage => !string.IsNullOrEmpty(_validationMessage);
        public bool HasActivityDescription => !string.IsNullOrEmpty(_activityDescription);
        public bool CanHaveCanton        => !string.IsNullOrEmpty(_selectedProvince);
        public bool CanHaveDistrict      => !string.IsNullOrEmpty(_selectedCanton);
        public bool CanHaveBarrio        => !string.IsNullOrEmpty(_selectedDistrict);
        public Keyboard ClientIdKeyboard => MapIdentificationTypeToCode(IdType) == "05"
            ? Keyboard.Default
            : Keyboard.Numeric;

        // ──────── Computed (double) ────────

        public double CantonOpacity   => CanHaveCanton   ? 1.0 : 0.45;
        public double DistrictOpacity => CanHaveDistrict ? 1.0 : 0.45;
        public double BarrioOpacity   => CanHaveBarrio   ? 1.0 : 0.45;

        // ──────── Computed (string) ────────

        public string SyncText => _syncStatus switch
        {
            SyncStatus.Syncing  => "Sincronizando...",
            SyncStatus.Synced   => "Sincronizado ✓",
            SyncStatus.NotFound => "No encontrado",
            _                   => "🔄 Sincronizar"
        };
        public string ClientStatusTitle => _syncStatus switch
        {
            SyncStatus.Syncing => "Buscando cliente...",
            SyncStatus.NotFound => "No se pudo completar la consulta",
            SyncStatus.Synced when IsExistingCustomer => "Cliente existente listo para actualizar",
            SyncStatus.Synced when !string.IsNullOrWhiteSpace(ClientId) => "Cliente listo para guardar",
            _ => "Ingrese la identificación para continuar"
        };
        public string ClientStatusHint => IsExistingCustomer
            ? "Ya existe en su base. Revise los datos, corrija lo necesario y luego actualice."
            : _syncStatus switch
            {
                SyncStatus.Syncing => "Buscamos primero en su base y luego en Hacienda para completar información automáticamente.",
                SyncStatus.Synced when !string.IsNullOrWhiteSpace(ClientId) => "No existe como cliente guardado. Complete los datos faltantes y luego guarde.",
                SyncStatus.NotFound => "No fue posible traer datos automáticos. Puede completar la información manualmente.",
                _ => "Ingrese la identificación y sincronice para saber si es un cliente nuevo o existente."
            };
        public string SaveButtonText => IsExistingCustomer ? "Actualizar" : "Guardar";
        public string SaveAndReturnButtonText => IsExistingCustomer ? "Actualizar y facturar" : "Guardar y facturar";
        public string SaveActionTitle => IsExistingCustomer ? "Actualizar cliente" : "Guardar cliente";
        public string TaxSectionTitle => IsReceiver
            ? "Datos para factura electrónica"
            : "Factura electrónica opcional";
        public string TaxSectionHint => IsReceiver
            ? "Complete esta sección solo si el cliente recibirá la factura por correo electrónico."
            : "Actívelo solo si este cliente necesita factura electrónica o envío por correo.";
        public string SaveActionHint => IsExistingCustomer
            ? "Se actualizará la información de este cliente actual."
            : "Se creará un cliente nuevo con la información ingresada.";

        // ──────── Computed (Color) — Sync button ────────

        public Color SyncButtonBackground => _syncStatus switch
        {
            SyncStatus.Syncing  => Color.FromArgb("#9CA3AF"),
            SyncStatus.NotFound => Color.FromArgb("#EF4444"),
            _                   => Color.FromArgb("#22C55E")
        };
        public Color SyncButtonTextColor => Colors.White;
        public Color ClientStatusBackgroundColor => _syncStatus switch
        {
            SyncStatus.Syncing => Color.FromArgb("#EFF6FF"),
            SyncStatus.NotFound => Color.FromArgb("#FFFBEB"),
            SyncStatus.Synced when IsExistingCustomer => Color.FromArgb("#ECFDF5"),
            SyncStatus.Synced => Color.FromArgb("#F0FDF4"),
            _ => Color.FromArgb("#F8FAFC")
        };
        public Color ClientStatusBorderColor => _syncStatus switch
        {
            SyncStatus.Syncing => Color.FromArgb("#93C5FD"),
            SyncStatus.NotFound => Color.FromArgb("#FCD34D"),
            SyncStatus.Synced when IsExistingCustomer => Color.FromArgb("#86EFAC"),
            SyncStatus.Synced => Color.FromArgb("#BBF7D0"),
            _ => Color.FromArgb("#CBD5E1")
        };
        public Color ClientStatusTitleColor => _syncStatus switch
        {
            SyncStatus.Syncing => Color.FromArgb("#1D4ED8"),
            SyncStatus.NotFound => Color.FromArgb("#92400E"),
            SyncStatus.Synced when IsExistingCustomer => Color.FromArgb("#166534"),
            SyncStatus.Synced => Color.FromArgb("#15803D"),
            _ => Color.FromArgb("#334155")
        };
        public Color ValidationMessageTextColor => _validationMessageKind switch
        {
            FormMessageKind.Info => Color.FromArgb("#065F46"),
            FormMessageKind.Warning => Color.FromArgb("#92400E"),
            FormMessageKind.Error => Color.FromArgb("#B91C1C"),
            _ => Color.FromArgb("#475569")
        };
        public Color ValidationMessageBackgroundColor => _validationMessageKind switch
        {
            FormMessageKind.Info => Color.FromArgb("#ECFDF5"),
            FormMessageKind.Warning => Color.FromArgb("#FFFBEB"),
            FormMessageKind.Error => Color.FromArgb("#FEF2F2"),
            _ => Color.FromArgb("#F8FAFC")
        };
        public Color ValidationMessageBorderColor => _validationMessageKind switch
        {
            FormMessageKind.Info => Color.FromArgb("#86EFAC"),
            FormMessageKind.Warning => Color.FromArgb("#FCD34D"),
            FormMessageKind.Error => Color.FromArgb("#FCA5A5"),
            _ => Color.FromArgb("#CBD5E1")
        };

        // ──────── Collections ────────

        public ObservableCollection<string> Provinces { get; } = new();
        public ObservableCollection<string> Cantons   { get; } = new();
        public ObservableCollection<string> Districts { get; } = new();
        public ObservableCollection<string> Barrios   { get; } = new();
        public ObservableCollection<string> CustomerTypes { get; } = new();

        private readonly List<ProvinciaNode> _provinciasData = new();

        public ObservableCollection<string> IdentificationTypes { get; } = new();

        // ──────── Commands ────────

        public ICommand SyncCommand           { get; }
        public ICommand SaveCommand           { get; }
        public ICommand SaveAndReturnCommand  { get; }
        public ICommand CancelCommand         { get; }
        public ICommand SearchActivityCommand { get; }

        public ClienteViewModel(IClienteService clienteService, IDialogService dialogService, AppStore appStore)
        {
            _clienteService = clienteService;
            _dialogService = dialogService;
            _appStore = appStore;
            SyncCommand           = new Command(async () => await ExecuteSync());
            SaveCommand           = new Command(async () => await ExecuteSave());
            SaveAndReturnCommand  = new Command(async () => await ExecuteSaveAndReturn());
            CancelCommand         = new Command(async () => await ExecuteCancel());
            SearchActivityCommand = new Command(async () => await ExecuteSearchActivity());

            _ = LoadLocationsAsync();
            _ = LoadCustomerTypesAsync();
            _ = LoadIdentificationTypesAsync();
        }

        // ──────── Data Loaders ────────

        private async Task LoadLocationsAsync()
        {
            try
            {
                using var stream = await FileSystem.OpenAppPackageFileAsync("ubicacion_cr_v44.json");
                using var reader = new StreamReader(stream);
                var json = await reader.ReadToEndAsync();
                LoadLocationsFromJson(json);
            }
            catch
            {
                await _dialogService.AlertAsync("Advertencia", "No se pudieron cargar las ubicaciones geográficas. Las listas de provincia, cantón y distrito estarán vacías.", "OK");
            }

            RefreshProvinceCollection(_provinciasData);

            if (_pendingLocationModel is not null)
            {
                ApplyLocationFromModel(_pendingLocationModel);
                _pendingLocationModel = null;
            }
        }

        private void LoadCantons(string? province)
        {
            Cantons.Clear();
            if (string.IsNullOrEmpty(province)) return;

            var provincia = _provinciasData.FirstOrDefault(p => string.Equals(p.Nombre, province, StringComparison.OrdinalIgnoreCase));
            if (provincia is null) return;

            foreach (var c in provincia.Cantones.Select(c => c.Nombre).OrderBy(x => x))
                Cantons.Add(c);
        }

        private void LoadDistricts(string? canton)
        {
            Districts.Clear();
            if (string.IsNullOrEmpty(canton)) return;

            var provincia = _provinciasData.FirstOrDefault(p => string.Equals(p.Nombre, SelectedProvince, StringComparison.OrdinalIgnoreCase));
            var cantonNode = provincia?.Cantones.FirstOrDefault(c => string.Equals(c.Nombre, canton, StringComparison.OrdinalIgnoreCase));
            if (cantonNode is null) return;

            foreach (var d in cantonNode.Distritos.Select(d => d.Nombre).OrderBy(x => x))
                Districts.Add(d);
        }

        private void LoadBarrios(string? province, string? canton, string? district)
        {
            Barrios.Clear();
            if (string.IsNullOrWhiteSpace(province) || string.IsNullOrWhiteSpace(canton) || string.IsNullOrWhiteSpace(district))
                return;

            var provinciaNode = _provinciasData.FirstOrDefault(p => string.Equals(p.Nombre, province, StringComparison.OrdinalIgnoreCase));
            var cantonNode = provinciaNode?.Cantones.FirstOrDefault(c => string.Equals(c.Nombre, canton, StringComparison.OrdinalIgnoreCase));
            var distritoNode = cantonNode?.Distritos.FirstOrDefault(d => string.Equals(d.Nombre, district, StringComparison.OrdinalIgnoreCase));
            if (distritoNode is null) return;

            foreach (var b in distritoNode.Barrios.OrderBy(x => x))
                Barrios.Add(b);
        }

        private void LoadLocationsFromJson(string json)
        {
            _provinciasData.Clear();

            var root = JsonNode.Parse(json)?["provincias"]?.AsObject();
            if (root is null) return;

            foreach (var provinciaKvp in root)
            {
                var provinciaNode = provinciaKvp.Value?.AsObject();
                if (provinciaNode is null) continue;

                var provincia = new ProvinciaNode
                {
                    Codigo = provinciaKvp.Key,
                    Nombre = provinciaNode["nombre"]?.GetValue<string>() ?? string.Empty
                };

                var cantonesNode = provinciaNode["cantones"]?.AsObject();
                if (cantonesNode is not null)
                {
                    foreach (var cantonKvp in cantonesNode)
                    {
                        var cantonNode = cantonKvp.Value?.AsObject();
                        if (cantonNode is null) continue;

                        var canton = new CantonNode
                        {
                            Codigo = cantonKvp.Key,
                            Nombre = cantonNode["nombre"]?.GetValue<string>() ?? string.Empty
                        };

                        var distritosNode = cantonNode["distritos"]?.AsObject();
                        if (distritosNode is not null)
                        {
                            foreach (var distritoKvp in distritosNode)
                            {
                                var distritoNode = distritoKvp.Value?.AsObject();
                                if (distritoNode is null) continue;

                                var distrito = new DistritoNode
                                {
                                    Codigo = distritoKvp.Key,
                                    Nombre = distritoNode["nombre"]?.GetValue<string>() ?? string.Empty
                                };

                                var barriosNode = distritoNode["barrios"]?.AsObject();
                                if (barriosNode is not null)
                                {
                                    foreach (var barrioKvp in barriosNode)
                                    {
                                        var barrio = barrioKvp.Value?.GetValue<string>();
                                        if (!string.IsNullOrWhiteSpace(barrio))
                                            distrito.Barrios.Add(barrio.Trim());
                                    }
                                }

                                canton.Distritos.Add(distrito);
                            }
                        }

                        provincia.Cantones.Add(canton);
                    }
                }

                _provinciasData.Add(provincia);
            }
        }

        private void ApplyLocationSearch(string? search)
        {
            if (_provinciasData.Count == 0) return;

            if (string.IsNullOrWhiteSpace(search))
            {
                RefreshProvinceCollection(_provinciasData);
                return;
            }

            var term = Normalize(search);
            var filtered = _provinciasData.Where(p =>
                Normalize(p.Nombre).Contains(term) ||
                p.Cantones.Any(c => Normalize(c.Nombre).Contains(term) ||
                                    c.Distritos.Any(d => Normalize(d.Nombre).Contains(term) ||
                                                         d.Barrios.Any(b => Normalize(b).Contains(term))))).ToList();

            RefreshProvinceCollection(filtered);

            var singleCanton = filtered
                .SelectMany(p => p.Cantones.Select(c => new { Provincia = p.Nombre, Canton = c.Nombre }))
                .FirstOrDefault(x => Normalize(x.Canton).Contains(term));

            if (singleCanton is not null)
            {
                SelectedProvince = singleCanton.Provincia;
                SelectedCanton = singleCanton.Canton;
            }
        }

        private void RefreshProvinceCollection(IEnumerable<ProvinciaNode> provincias)
        {
            Provinces.Clear();
            foreach (var p in provincias.Select(x => x.Nombre).Distinct().OrderBy(x => x))
                Provinces.Add(p);
        }

        private static string Normalize(string? value)
            => string.Concat((value ?? string.Empty)
                .Normalize(NormalizationForm.FormD)
                .Where(c => CharUnicodeInfo.GetUnicodeCategory(c) != UnicodeCategory.NonSpacingMark))
                .ToUpperInvariant();

        private async Task LoadCustomerTypesAsync()
        {
            var tipos = await _clienteService.ObtenerTiposClienteAsync();
            CustomerTypes.Clear();
            foreach (var tipo in tipos)
                CustomerTypes.Add(tipo);

            if (string.IsNullOrWhiteSpace(SelectedCustomerType) && CustomerTypes.Count > 0)
                SelectedCustomerType = CustomerTypes[0];
        }

        private async Task LoadIdentificationTypesAsync()
        {
            var tipos = await _clienteService.ObtenerTiposIdentificacionAsync();
            IdentificationTypes.Clear();
            foreach (var tipo in tipos)
                IdentificationTypes.Add(tipo);

            if (string.IsNullOrWhiteSpace(IdType) && IdentificationTypes.Count > 0)
                IdType = IdentificationTypes[0];
        }

        // ──────── Command Handlers ────────

        private async Task ExecuteSync()
        {
            if (string.IsNullOrWhiteSpace(ClientId)) return;

            LocationSearch = string.Empty;
            _lastAutoSyncedClientId = ClientId;

            SyncStatus = SyncStatus.Syncing;
            ClearValidationMessage();

            var local = await _clienteService.BuscarPorIdAsync(ClientId);
            if (local is not null)
            {
                SyncStatus = SyncStatus.Synced;
                _lastSyncedClientId = ClientId;

                var wantsToUpdate = await _dialogService.ConfirmAsync(
                    "Cliente existente",
                    "Este cliente ya existe. ¿Desea actualizar sus datos?",
                    "Sí, actualizar",
                    "No, usar en compra");

                LoadFromModel(local);
                IsExistingCustomer = true;

                if (!wantsToUpdate)
                {
                    _existingCustomerUpdateConfirmed = false;
                    SetValidationMessage("Cliente existente seleccionado para esta compra.", FormMessageKind.Info);
                    SelectCurrentClient();
                    await _dialogService.AlertAsync(
                        "Cliente seleccionado",
                        "Este cliente ya quedó seleccionado para realizar la compra.",
                        "OK");
                    await TryNavigateBack();
                    return;
                }

                _existingCustomerUpdateConfirmed = true;
                SetValidationMessage("Cliente existente cargado para edición.", FormMessageKind.Info);

                var remote = await _clienteService.SincronizarHaciendaAsync(ClientId);
                if (remote is not null)
                    MergeRemoteTaxData(remote);

                // Give the UI dispatcher one frame to apply the deferred barrio selection.
                await Task.Delay(50);

                var hints = new List<string>();
                if (string.IsNullOrWhiteSpace(SelectedProvince) || string.IsNullOrWhiteSpace(SelectedBarrio))
                    hints.Add("ubicación incompleta");
                if (string.IsNullOrWhiteSpace(ActivityCode))
                    hints.Add("código de actividad vacío (ingrese manualmente)");

                SetValidationMessage(
                    hints.Count > 0
                        ? $"Cliente existente cargado. Revise estos datos antes de actualizar: {string.Join(", ", hints)}."
                        : "Cliente existente cargado para edición.",
                    hints.Count > 0 ? FormMessageKind.Warning : FormMessageKind.Info);

                return;
            }

            var result = await _clienteService.SincronizarHaciendaAsync(ClientId);

            if (result is not null)
            {
                LoadFromModel(result);
                IsExistingCustomer = false;
                _existingCustomerUpdateConfirmed = false;
                SyncStatus = SyncStatus.Synced;
                ClearValidationMessage();
                _lastSyncedClientId = ClientId;
            }
            else
            {
                IsExistingCustomer = false;
                _existingCustomerUpdateConfirmed = false;
                SyncStatus = SyncStatus.NotFound;
                SetValidationMessage("No fue posible consultar Hacienda. Puede completar los datos manualmente.", FormMessageKind.Warning);
            }
        }

        private async Task ExecuteSearchActivity()
        {
            if (string.IsNullOrWhiteSpace(ActivityCode)) return;

            ActivityDescription = await _clienteService.BuscarActividadAsync(ActivityCode);
        }

        private async Task ExecuteSave()
        {
            System.Diagnostics.Debug.WriteLine($"ExecuteSave intento: Id={ClientId}, Provincia={SelectedProvince}, Canton={SelectedCanton}, Distrito={SelectedDistrict}, Barrio={SelectedBarrio}, ActivityCode={ActivityCode}");
            if (!Validate())
            {
                System.Diagnostics.Debug.WriteLine($"ExecuteSave validación falló: {ValidationMessage}");
                return;
            }

            if (IsExistingCustomer && !_existingCustomerUpdateConfirmed)
            {
                var confirm = await _dialogService.ConfirmAsync(
                    "Cliente existente",
                    "Este cliente ya existe. ¿Desea actualizar la información?",
                    "Actualizar", "Cancelar");
                if (!confirm) return;
                _existingCustomerUpdateConfirmed = true;
            }

            var saved = await _clienteService.GuardarAsync(ToModel());
            if (saved)
            {
                SelectCurrentClient();
                await _dialogService.AlertAsync("✅ Guardado", IsExistingCustomer ? "Cliente actualizado correctamente." : "Cliente guardado correctamente.", "OK");
                await TryNavigateBack();
            }
            else
            {
                SetValidationMessage("No se pudo guardar el cliente. Verifique la conexión con el servidor.", FormMessageKind.Error);
            }
        }

        private async Task ExecuteSaveAndReturn()
        {
            System.Diagnostics.Debug.WriteLine($"ExecuteSaveAndReturn intento: Id={ClientId}, Provincia={SelectedProvince}, Canton={SelectedCanton}, Distrito={SelectedDistrict}, Barrio={SelectedBarrio}, ActivityCode={ActivityCode}");
            if (!Validate())
            {
                System.Diagnostics.Debug.WriteLine($"ExecuteSaveAndReturn validación falló: {ValidationMessage}");
                return;
            }

            if (IsExistingCustomer && !_existingCustomerUpdateConfirmed)
            {
                var confirm = await _dialogService.ConfirmAsync(
                    "Cliente existente",
                    "Este cliente ya existe. ¿Desea actualizar la información?",
                    "Actualizar", "Cancelar");
                if (!confirm) return;
                _existingCustomerUpdateConfirmed = true;
            }

            var saved = await _clienteService.GuardarAsync(ToModel());
            if (saved)
            {
                SelectCurrentClient();
                await _dialogService.AlertAsync("✅ Guardado", IsExistingCustomer ? "Cliente actualizado. Regresando a facturar." : "Cliente guardado. Regresando a facturar.", "OK");
                await TryNavigateBack();
            }
            else
            {
                SetValidationMessage("No se pudo guardar el cliente. Verifique la conexión con el servidor.", FormMessageKind.Error);
            }
        }

        private ClienteModel ToModel() => new()
        {
            ClientId            = ClientId,
            IdType              = IdType,
            Name                = Name,
            IsReceiver          = IsReceiver,
            Phone               = Phone,
            Email               = Email,
            Email2              = string.IsNullOrWhiteSpace(Email2) ? Email : Email2,
            Province            = SelectedProvince,
            Canton              = SelectedCanton,
            District            = SelectedDistrict,
            Barrio              = SelectedBarrio,
            CustomerType        = SelectedCustomerType,
            Address             = Address,
            ActivityCode        = ActivityCode,
            ActivityCodes       = ParseActivityCodes(ActivityCode),
            ActivityDescription = ActivityDescription
        };

        private void SelectCurrentClient()
        {
            if (string.IsNullOrWhiteSpace(ClientId))
                return;

            _appStore.Dispatch(new SetCurrentClientAction(ClientId.Trim(), (Name ?? string.Empty).Trim(), IsReceiver, SelectedCustomerType ?? string.Empty));
        }

        private void LoadFromModel(ClienteModel model)
        {
            ClientId = model.ClientId;
            IdType = ResolveLoadedIdType(model);
            Name = model.Name;
            IsReceiver = model.IsReceiver;
            Phone = model.Phone;
            Email = model.Email;
            Email2 = model.Email2;

            Address = model.Address;
            SelectedCustomerType = model.CustomerType;

            var parsed = model.ActivityCodes?.Count > 0
                ? model.ActivityCodes
                : ParseActivityCodes(model.ActivityCode);

            ActivityCode = string.Join(", ", parsed);
            ActivityDescription = model.ActivityDescription;
            LocationSearch = string.Empty;

            if (_provinciasData.Count == 0)
            {
                _pendingLocationModel = model;
            }
            else
            {
                ApplyLocationFromModel(model);
                _pendingLocationModel = null;
            }
        }

        private void MergeRemoteTaxData(ClienteModel model)
        {
            var remoteIdType = ResolveLoadedIdType(model);
            if (!string.Equals(IdType, remoteIdType, StringComparison.Ordinal))
                IdType = remoteIdType;

            if (string.IsNullOrWhiteSpace(ActivityCode))
            {
                var parsed = model.ActivityCodes?.Count > 0
                    ? model.ActivityCodes
                    : ParseActivityCodes(model.ActivityCode);
                ActivityCode = string.Join(", ", parsed);
            }

            if (string.IsNullOrWhiteSpace(ActivityDescription))
                ActivityDescription = model.ActivityDescription;

            if (string.IsNullOrWhiteSpace(Email))
                Email = model.Email;

            if (string.IsNullOrWhiteSpace(Phone))
                Phone = model.Phone;
        }

        private string ResolveLoadedIdType(ClienteModel model)
        {
            if (!string.IsNullOrWhiteSpace(model.IdType) &&
                !string.Equals(model.IdType, "Cédula Física", StringComparison.OrdinalIgnoreCase))
            {
                return model.IdType;
            }

            if (string.Equals(model.ClientId, ClientId, StringComparison.Ordinal) &&
                !string.IsNullOrWhiteSpace(IdType) &&
                !string.Equals(IdType, "Cédula Física", StringComparison.OrdinalIgnoreCase))
            {
                return IdType;
            }

            var digits = new string((model.ClientId ?? string.Empty).Where(char.IsDigit).ToArray());
            return digits.Length switch
            {
                9 => "Cédula Física",
                10 => "Cédula Jurídica",
                12 => "DIMEX",
                _ => string.IsNullOrWhiteSpace(model.IdType) ? "Cédula Física" : model.IdType
            };
        }

        private static string BuildLocationSummary(string? province, string? canton, string? district, string? barrio, string? address)
        {
            var parts = new[] { province, canton, district, barrio }
                .Where(x => !string.IsNullOrWhiteSpace(x))
                .Select(x => x!.Trim())
                .ToList();

            if (!string.IsNullOrWhiteSpace(address))
                parts.Add(address.Trim());

            return string.Join(", ", parts.Distinct(StringComparer.OrdinalIgnoreCase));
        }

        private void ApplyLocationFromModel(ClienteModel model)
        {
            var provincia = FindProvinceName(model.Province, model.Address, model.Canton, model.District, model.Barrio);
            var canton = FindCantonName(provincia, model.Canton, model.Address, model.District, model.Barrio);
            var distrito = FindDistrictName(provincia, canton, model.District, model.Address, model.Barrio);
            var barrio = FindBarrioName(provincia, canton, distrito, model.Barrio, model.Address);

            _selectedProvince = provincia;
            OnPropertyChanged(nameof(SelectedProvince));
            LoadCantons(provincia);
            OnPropertyChanged(nameof(CanHaveCanton));
            OnPropertyChanged(nameof(CantonOpacity));

            _selectedCanton = canton;
            OnPropertyChanged(nameof(SelectedCanton));
            LoadDistricts(canton);
            OnPropertyChanged(nameof(CanHaveDistrict));
            OnPropertyChanged(nameof(DistrictOpacity));

            _selectedDistrict = distrito;
            OnPropertyChanged(nameof(SelectedDistrict));
            LoadBarrios(provincia, canton, distrito);
            OnPropertyChanged(nameof(CanHaveBarrio));
            OnPropertyChanged(nameof(BarrioOpacity));

            // Defer barrio selection to next dispatcher frame so the Picker's
            // ItemsSource Reset (from Barrios.Clear) doesn't wipe SelectedItem afterwards.
            _selectedBarrio = null;
            OnPropertyChanged(nameof(SelectedBarrio));
            if (barrio is not null)
            {
                MainThread.BeginInvokeOnMainThread(() =>
                {
                    _selectedBarrio = barrio;
                    OnPropertyChanged(nameof(SelectedBarrio));
                });
            }

            System.Diagnostics.Debug.WriteLine($"ApplyLocation: P={provincia}, C={canton}, D={distrito}, B={barrio}, Barrios={Barrios.Count}");
        }

        private string? FindProvinceName(params string?[] values)
        {
            var normalizedValues = values
                .Where(v => !string.IsNullOrWhiteSpace(v))
                .Select(Normalize)
                .ToList();

            var directMatch = _provinciasData
                .Select(p => p.Nombre)
                .FirstOrDefault(nombre => normalizedValues.Any(v => v.Contains(Normalize(nombre))));

            if (!string.IsNullOrWhiteSpace(directMatch))
                return directMatch;

            foreach (var provincia in _provinciasData)
            {
                if (provincia.Cantones.Any(c => normalizedValues.Any(v => v.Contains(Normalize(c.Nombre)))))
                    return provincia.Nombre;

                if (provincia.Cantones.Any(c => c.Distritos.Any(d => normalizedValues.Any(v => v.Contains(Normalize(d.Nombre))))))
                    return provincia.Nombre;

                if (provincia.Cantones.Any(c => c.Distritos.Any(d => d.Barrios.Any(b => normalizedValues.Any(v => v.Contains(Normalize(b)))))))
                    return provincia.Nombre;
            }

            return null;
        }

        private string? FindCantonName(string? province, params string?[] values)
        {
            if (string.IsNullOrWhiteSpace(province))
                return null;

            var provinciaNode = _provinciasData.FirstOrDefault(p => string.Equals(p.Nombre, province, StringComparison.OrdinalIgnoreCase));
            if (provinciaNode is null)
                return null;

            var normalizedValues = values
                .Where(v => !string.IsNullOrWhiteSpace(v))
                .Select(Normalize)
                .ToList();

            var directMatch = provinciaNode.Cantones
                .Select(c => c.Nombre)
                .FirstOrDefault(nombre => normalizedValues.Any(v => v.Contains(Normalize(nombre))));

            if (!string.IsNullOrWhiteSpace(directMatch))
                return directMatch;

            foreach (var canton in provinciaNode.Cantones)
            {
                if (canton.Distritos.Any(d => normalizedValues.Any(v => v.Contains(Normalize(d.Nombre)))))
                    return canton.Nombre;

                if (canton.Distritos.Any(d => d.Barrios.Any(b => normalizedValues.Any(v => v.Contains(Normalize(b))))))
                    return canton.Nombre;
            }

            return null;
        }

        private string? FindDistrictName(string? province, string? canton, params string?[] values)
        {
            if (string.IsNullOrWhiteSpace(province) || string.IsNullOrWhiteSpace(canton))
                return null;

            var provinciaNode = _provinciasData.FirstOrDefault(p => string.Equals(p.Nombre, province, StringComparison.OrdinalIgnoreCase));
            var cantonNode = provinciaNode?.Cantones.FirstOrDefault(c => string.Equals(c.Nombre, canton, StringComparison.OrdinalIgnoreCase));
            if (cantonNode is null)
                return null;

            var normalizedValues = values
                .Where(v => !string.IsNullOrWhiteSpace(v))
                .Select(Normalize)
                .ToList();

            var directMatch = cantonNode.Distritos
                .Select(d => d.Nombre)
                .FirstOrDefault(nombre => normalizedValues.Any(v => v.Contains(Normalize(nombre))));

            if (!string.IsNullOrWhiteSpace(directMatch))
                return directMatch;

            foreach (var distrito in cantonNode.Distritos)
            {
                if (distrito.Barrios.Any(b => normalizedValues.Any(v => v.Contains(Normalize(b)))))
                    return distrito.Nombre;
            }

            return null;
        }

        private string? FindBarrioName(string? province, string? canton, string? district, params string?[] values)
        {
            if (string.IsNullOrWhiteSpace(province) || string.IsNullOrWhiteSpace(canton) || string.IsNullOrWhiteSpace(district))
                return null;

            var provinciaNode = _provinciasData.FirstOrDefault(p => string.Equals(p.Nombre, province, StringComparison.OrdinalIgnoreCase));
            var cantonNode = provinciaNode?.Cantones.FirstOrDefault(c => string.Equals(c.Nombre, canton, StringComparison.OrdinalIgnoreCase));
            var distritoNode = cantonNode?.Distritos.FirstOrDefault(d => string.Equals(d.Nombre, district, StringComparison.OrdinalIgnoreCase));
            if (distritoNode is null)
                return null;

            var rawValues = values
                .Where(v => !string.IsNullOrWhiteSpace(v))
                .Select(v => v!.Trim())
                .ToList();

            System.Diagnostics.Debug.WriteLine($"FindBarrio: distrito={district}, rawValues=[{string.Join(", ", rawValues)}], catalogBarrios={distritoNode.Barrios.Count}");

            var exactMatch = distritoNode.Barrios
                .FirstOrDefault(b => rawValues.Any(v => string.Equals(b, v, StringComparison.OrdinalIgnoreCase)));
            if (exactMatch is not null)
                return exactMatch;

            var normalizedValues = rawValues.Select(Normalize).ToList();

            var normalizedMatch = distritoNode.Barrios
                .FirstOrDefault(nombre => normalizedValues.Any(v => Normalize(nombre) == v));
            if (normalizedMatch is not null)
                return normalizedMatch;

            return distritoNode.Barrios
                .FirstOrDefault(nombre => normalizedValues.Any(v => v.Contains(Normalize(nombre))));
        }

        private async Task ExecuteCancel()
        {
            ClearValidationMessage();
            await TryNavigateBack();
        }

        private static async Task TryNavigateBack()
        {
            try { await Shell.Current.GoToAsync(".."); }
            catch { /* Root page — no parent to navigate to */ }
        }

        private bool Validate()
        {
            var errors = new List<string>();
           

            if (string.IsNullOrWhiteSpace(ClientId))
                errors.Add("• El ID del cliente es requerido.");
            else
            {
                var tipoCodigo = MapIdentificationTypeToCode(IdType);
                if (!IdValidatorCR.ValidateFinal(tipoCodigo, ClientId, out var idError) && !string.IsNullOrWhiteSpace(idError))
                    errors.Add($"• {idError}");
            }

            if (string.IsNullOrWhiteSpace(Name))
                errors.Add("• El nombre del cliente es requerido.");

            if (IsReceiver)
            {
                if (string.IsNullOrWhiteSpace(Phone))
                    errors.Add("• El teléfono es requerido.");
                else if (!IsValidPhone(Phone))
                    errors.Add("• El teléfono no es válido (ej: 8888-8888).");

                if (string.IsNullOrWhiteSpace(Email))
                    errors.Add("• El correo electrónico es requerido.");
                else if (!IsValidEmail(Email))
                    errors.Add("• El correo electrónico no es válido (ej: usuario@dominio.com).");

                if (!string.IsNullOrWhiteSpace(Email2) && !IsValidEmail(Email2))
                    errors.Add("• El correo electrónico #2 no es válido.");

                if (string.IsNullOrWhiteSpace(SelectedProvince))
                    errors.Add("• La provincia es requerida.");

                if (string.IsNullOrWhiteSpace(SelectedCanton))
                    errors.Add("• El cantón es requerido.");

                if (string.IsNullOrWhiteSpace(SelectedDistrict))
                    errors.Add("• El distrito es requerido.");

                if (string.IsNullOrWhiteSpace(Address))
                    errors.Add("• La dirección es requerida.");

                if (string.IsNullOrWhiteSpace(SelectedBarrio))
                    errors.Add("• El barrio es requerido.");

                if (string.IsNullOrWhiteSpace(SelectedCustomerType))
                    errors.Add("• El tipo de cliente es requerido.");

                if (string.IsNullOrWhiteSpace(ActivityCode))
                    errors.Add("• El código de actividad es requerido.");
                else
                {
                    var codigos = ParseActivityCodes(ActivityCode);
                    if (codigos.Count == 0)
                        errors.Add("• Debe indicar al menos un código de actividad válido.");
                    else if (codigos.Count > 5)
                        errors.Add("• Se permiten máximo 5 códigos de actividad.");
                    else if (codigos.Any(c => !Regex.IsMatch(c, "^\\d{6}$")))
                        errors.Add("• Cada código de actividad debe tener 6 dígitos.");
                }
            }

            SetValidationMessage(
                errors.Count > 0 ? string.Join("\n", errors) : string.Empty,
                errors.Count > 0 ? FormMessageKind.Error : FormMessageKind.None);

            return errors.Count == 0;
        }

        private void ClearValidationMessage()
            => SetValidationMessage(string.Empty, FormMessageKind.None);

        private void SetValidationMessage(string message, FormMessageKind kind)
        {
            _validationMessageKind = string.IsNullOrWhiteSpace(message) ? FormMessageKind.None : kind;
            ValidationMessage = message;
            OnPropertyChanged(nameof(ValidationMessageTextColor));
            OnPropertyChanged(nameof(ValidationMessageBackgroundColor));
            OnPropertyChanged(nameof(ValidationMessageBorderColor));
        }

        private static readonly Regex _emailRegex =
            new(@"^[^@\s]+@[^@\s]+\.[^@\s]{2,}$", RegexOptions.Compiled | RegexOptions.IgnoreCase);

        private static readonly Regex _phoneRegex =
            new(@"^[\d\s\-\+\(\)]{7,15}$", RegexOptions.Compiled);

        private static bool IsValidEmail(string email) => _emailRegex.IsMatch(email.Trim());
        private static bool IsValidPhone(string phone) => _phoneRegex.IsMatch(phone.Trim());

        private static List<string> ParseActivityCodes(string value)
        {
            if (string.IsNullOrWhiteSpace(value)) return new List<string>();

            return value
                .Split(new[] { ',', ';' }, StringSplitOptions.RemoveEmptyEntries)
                .Select(c => c.Trim())
                .Where(c => !string.IsNullOrWhiteSpace(c))
                .Distinct(StringComparer.InvariantCultureIgnoreCase)
                .ToList();
        }

        private async Task TryAutoSyncAsync(string clientId)
        {
            if (_isAutoSyncInProgress || string.IsNullOrWhiteSpace(clientId) || string.Equals(_lastAutoSyncedClientId, clientId, StringComparison.Ordinal) && IsSyncing)
                return;

            _isAutoSyncInProgress = true;
            try
            {
                await Task.Delay(250);

                if (!string.Equals(ClientId, clientId, StringComparison.Ordinal) || IsSyncing)
                    return;

                await ExecuteSync();
            }
            finally
            {
                _isAutoSyncInProgress = false;
            }
        }

        private bool ShouldAutoSyncClientId(string clientId)
        {
            var expectedLength = GetExpectedIdLength(IdType);
            if (expectedLength <= 0)
                return false;

            return clientId.Length == expectedLength &&
                   !string.Equals(clientId, _lastSyncedClientId, StringComparison.Ordinal) &&
                   !string.Equals(clientId, _lastAutoSyncedClientId, StringComparison.Ordinal);
        }

        private static string MapIdentificationTypeToCode(string idType)
        {
            return idType switch
            {
                "Cédula Física" => "01",
                "Cédula Jurídica" => "02",
                "DIMEX" => "03",
                "NITE" => "04",
                "Extranjero No Domiciliado" => "05",
                "No Contribuyente" => "06",
                _ => string.Empty
            };
        }

        private static int GetExpectedIdLength(string idType)
        {
            return MapIdentificationTypeToCode(idType) switch
            {
                "01" => 9,
                "02" => 10,
                "03" => 12,
                "04" => 10,
                _ => 0
            };
        }

        private static string SanitizeClientIdByType(string value, string idType)
        {
            var input = value ?? string.Empty;
            var tipo = MapIdentificationTypeToCode(idType);

            if (tipo == "05")
            {
                var alnum = new string(input.Where(char.IsLetterOrDigit).ToArray());
                return alnum.Length > 20 ? alnum[..20] : alnum;
            }

            var digits = new string(input.Where(char.IsDigit).ToArray());
            var max = tipo switch
            {
                "01" => 9,
                "02" => 10,
                "03" => 12,
                "04" => 10,
                "06" => 20,
                _ => 20
            };

            return digits.Length > max ? digits[..max] : digits;
        }

        private sealed class ProvinciaNode
        {
            public string Codigo { get; set; } = string.Empty;
            public string Nombre { get; set; } = string.Empty;
            public List<CantonNode> Cantones { get; } = new();
        }

        private sealed class CantonNode
        {
            public string Codigo { get; set; } = string.Empty;
            public string Nombre { get; set; } = string.Empty;
            public List<DistritoNode> Distritos { get; } = new();
        }

        private sealed class DistritoNode
        {
            public string Codigo { get; set; } = string.Empty;
            public string Nombre { get; set; } = string.Empty;
            public List<string> Barrios { get; } = new();
        }

        // ──────── INotifyPropertyChanged ────────

        public event PropertyChangedEventHandler? PropertyChanged;

        protected void OnPropertyChanged([CallerMemberName] string? name = null)
            => PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(name));
    }
}
