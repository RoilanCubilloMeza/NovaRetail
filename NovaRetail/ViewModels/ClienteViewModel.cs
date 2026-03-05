using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Runtime.CompilerServices;
using System.Text.RegularExpressions;
using System.Windows.Input;
using NovaRetail.Data;
using NovaRetail.Models;

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
        private string _address = string.Empty;
        private string _activityCode = string.Empty;
        private string _activityDescription = string.Empty;
        private SyncStatus _syncStatus = SyncStatus.Idle;
        private string _validationMessage = string.Empty;


        public string ClientId
        {
            get => _clientId;
            set { _clientId = value; OnPropertyChanged(); }
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
            set { _selectedDistrict = value; OnPropertyChanged(); }
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

        // ──────── Computed (double) ────────

        public double CantonOpacity   => CanHaveCanton   ? 1.0 : 0.45;
        public double DistrictOpacity => CanHaveDistrict ? 1.0 : 0.45;

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

            LoadProvinces();
        }

        // ──────── Data Loaders ────────

        private void LoadProvinces()
        {
            foreach (var p in new[] { "San José", "Alajuela", "Cartago", "Heredia", "Guanacaste", "Puntarenas", "Limón" })
                Provinces.Add(p);
        }

        private void LoadCantons(string? province)
        {
            Cantons.Clear();
            if (string.IsNullOrEmpty(province)) return;

            var data = new Dictionary<string, string[]>
            {
                ["San José"]   = ["San José", "Escazú", "Desamparados", "Puriscal", "Tarrazú", "Aserrí", "Mora", "Goicoechea", "Santa Ana"],
                ["Alajuela"]   = ["Alajuela", "San Ramón", "Grecia", "San Mateo", "Atenas", "Naranjo", "Palmares", "Poás", "Orotina"],
                ["Cartago"]    = ["Cartago", "Paraíso", "La Unión", "Jiménez", "Turrialba", "Alvarado", "Oreamuno", "El Guarco"],
                ["Heredia"]    = ["Heredia", "Barva", "Santo Domingo", "Santa Bárbara", "San Rafael", "San Isidro", "Belén", "Flores"],
                ["Guanacaste"] = ["Liberia", "Nicoya", "Santa Cruz", "Bagaces", "Carrillo", "Cañas", "Abangares", "Tilarán"],
                ["Puntarenas"] = ["Puntarenas", "Esparza", "Buenos Aires", "Montes de Oro", "Osa", "Aguirre", "Golfito", "Coto Brus"],
                ["Limón"]      = ["Limón", "Pococí", "Siquirres", "Talamanca", "Matina", "Guácimo"]
            };

            if (data.TryGetValue(province, out var cantons))
                foreach (var c in cantons) Cantons.Add(c);
        }

        private void LoadDistricts(string? canton)
        {
            Districts.Clear();
            if (string.IsNullOrEmpty(canton)) return;

            // TODO: Replace with real district data from API/DB
            foreach (var suffix in new[] { "Centro", "Norte", "Sur", "Este", "Oeste" })
                Districts.Add($"{canton} {suffix}");
        }

        // ──────── Command Handlers ────────

        private async Task ExecuteSync()
        {
            if (string.IsNullOrWhiteSpace(ClientId)) return;

            SyncStatus = SyncStatus.Syncing;

            var result = await _clienteService.SincronizarHaciendaAsync(ClientId);

            if (result is not null)
            {
                Name       = result.Name;
                SyncStatus = SyncStatus.Synced;
            }
            else
            {
                SyncStatus = SyncStatus.NotFound;
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
            Address             = Address,
            ActivityCode        = ActivityCode,
            ActivityDescription = ActivityDescription
        };

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

                if (string.IsNullOrWhiteSpace(ActivityCode))
                    errors.Add("• El código de actividad es requerido.");
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

        // ──────── INotifyPropertyChanged ────────

        public event PropertyChangedEventHandler? PropertyChanged;

        protected void OnPropertyChanged([CallerMemberName] string? name = null)
            => PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(name));
    }
}
