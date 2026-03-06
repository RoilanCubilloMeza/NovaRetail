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

namespace NovaRetail.ViewModels
{
    public enum SyncStatus { Idle, Syncing, Synced, NotFound }

    public class ClienteViewModel : INotifyPropertyChanged
    {
        private readonly IClienteService _clienteService;

        private string _clientId = string.Empty;
        private string _idType = "Cédula Física";
        private string _name = string.Empty;
        private bool _isReceiver;
        private string _phone = string.Empty;
        private string _email = string.Empty;
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
        private string _lastSyncedClientId = string.Empty;


        public string ClientId
        {
            get => _clientId;
            set
            {
                var sanitized = SanitizeClientIdByType(value, IdType);
                if (_clientId == sanitized) return;
                _clientId = sanitized;
                OnPropertyChanged();
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

        // ──────── Computed (Color) — Sync button ────────

        public Color SyncButtonBackground => _syncStatus switch
        {
            SyncStatus.Syncing  => Color.FromArgb("#9CA3AF"),
            SyncStatus.NotFound => Color.FromArgb("#EF4444"),
            _                   => Color.FromArgb("#22C55E")
        };
        public Color SyncButtonTextColor => Colors.White;

        // ──────── Collections ────────

        public ObservableCollection<string> Provinces { get; } = new();
        public ObservableCollection<string> Cantons   { get; } = new();
        public ObservableCollection<string> Districts { get; } = new();
        public ObservableCollection<string> Barrios   { get; } = new();
        public ObservableCollection<string> CustomerTypes { get; } = new();

        private readonly List<ProvinciaNode> _provinciasData = new();

        public IReadOnlyList<string> IdentificationTypes { get; } = new[]
        {
            "Cédula Física",
            "Cédula Jurídica",
            "DIMEX",
            "NITE",
            "Extranjero No Domiciliado",
            "No Contribuyente"
        };

        // ──────── Commands ────────

        public ICommand SyncCommand           { get; }
        public ICommand SaveCommand           { get; }
        public ICommand SaveAndReturnCommand  { get; }
        public ICommand CancelCommand         { get; }
        public ICommand SearchActivityCommand { get; }

        public ClienteViewModel(IClienteService clienteService)
        {
            _clienteService = clienteService;
            SyncCommand           = new Command(async () => await ExecuteSync());
            SaveCommand           = new Command(async () => await ExecuteSave());
            SaveAndReturnCommand  = new Command(async () => await ExecuteSaveAndReturn());
            CancelCommand         = new Command(async () => await ExecuteCancel());
            SearchActivityCommand = new Command(async () => await ExecuteSearchActivity());

            _ = LoadLocationsAsync();
            _ = LoadCustomerTypesAsync();
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
                LoadFallbackLocations();
            }

            RefreshProvinceCollection(_provinciasData);
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

        private void LoadFallbackLocations()
        {
            _provinciasData.Clear();
            _provinciasData.Add(new ProvinciaNode { Codigo = "6", Nombre = "PUNTARENAS", Cantones = { new CantonNode { Codigo = "06", Nombre = "QUEPOS", Distritos = { new DistritoNode { Codigo = "01", Nombre = "QUEPOS", Barrios = { "QUEPOS centro", "Manuel Antonio", "Paquita" } }, new DistritoNode { Codigo = "02", Nombre = "SAVEGRE", Barrios = { "MATAPALO", "Hatillo", "Portalón" } } } } } });
            _provinciasData.Add(new ProvinciaNode { Codigo = "1", Nombre = "SAN JOSÉ", Cantones = { new CantonNode { Codigo = "01", Nombre = "SAN JOSÉ", Distritos = { new DistritoNode { Codigo = "01", Nombre = "CARMEN", Barrios = { "Amón", "Aranjuez", "Escalante" } } } } } });
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

        private static string Normalize(string value)
            => string.Concat(value
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

        // ──────── Command Handlers ────────

        private async Task ExecuteSync()
        {
            if (string.IsNullOrWhiteSpace(ClientId)) return;

            if (!string.Equals(_lastSyncedClientId, ClientId, StringComparison.Ordinal))
                LocationSearch = string.Empty;

            SyncStatus = SyncStatus.Syncing;
            ValidationMessage = string.Empty;

            var local = await _clienteService.BuscarPorIdAsync(ClientId);
            if (local is not null)
            {
                LoadFromModel(local);
                SyncStatus = SyncStatus.Synced;
                ValidationMessage = "Cliente existente cargado para edición.";
                _lastSyncedClientId = ClientId;
                return;
            }

            var result = await _clienteService.SincronizarHaciendaAsync(ClientId);

            if (result is not null)
            {
                LoadFromModel(result);
                SyncStatus = SyncStatus.Synced;
                ValidationMessage = string.Empty;
                _lastSyncedClientId = ClientId;
            }
            else
            {
                SyncStatus = SyncStatus.NotFound;
                ValidationMessage = "Consulta de API con Hacienda no disponible, ingrese los datos manualmente";
            }
        }

        private async Task ExecuteSearchActivity()
        {
            if (string.IsNullOrWhiteSpace(ActivityCode)) return;

            ActivityDescription = await _clienteService.BuscarActividadAsync(ActivityCode);
        }

        private async Task ExecuteSave()
        {
            if (!Validate()) return;

            await _clienteService.GuardarAsync(ToModel());
            await TryNavigateBack();
        }

        private async Task ExecuteSaveAndReturn()
        {
            if (!Validate()) return;

            await _clienteService.GuardarAsync(ToModel());
            await TryNavigateBack();
        }

        private ClienteModel ToModel() => new()
        {
            ClientId            = ClientId,
            IdType              = IdType,
            Name                = Name,
            IsReceiver          = IsReceiver,
            Phone               = Phone,
            Email               = Email,
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

        private void LoadFromModel(ClienteModel model)
        {
            ClientId = model.ClientId;
            IdType = model.IdType;
            Name = model.Name;
            IsReceiver = model.IsReceiver;
            Phone = model.Phone;
            Email = model.Email;

            SelectedProvince = model.Province;
            SelectedCanton = model.Canton;
            SelectedDistrict = model.District;
            SelectedBarrio = model.Barrio;

            Address = model.Address;
            SelectedCustomerType = model.CustomerType;

            var parsed = model.ActivityCodes?.Count > 0
                ? model.ActivityCodes
                : ParseActivityCodes(model.ActivityCode);

            ActivityCode = string.Join(", ", parsed);
            ActivityDescription = model.ActivityDescription;
        }

        private async Task ExecuteCancel()
        {
            ValidationMessage = string.Empty;
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

            ValidationMessage = errors.Count > 0
                ? string.Join("\n", errors)
                : string.Empty;

            return errors.Count == 0;
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
