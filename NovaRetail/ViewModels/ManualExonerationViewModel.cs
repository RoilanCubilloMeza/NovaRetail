using NovaRetail.Models;
using System.ComponentModel;
using System.Globalization;
using System.Runtime.CompilerServices;
using System.Windows.Input;

namespace NovaRetail.ViewModels
{
    public sealed class ExonerationDocumentType
    {
        public string Codigo { get; init; } = string.Empty;
        public string Descripcion { get; init; } = string.Empty;
        public override string ToString() => Descripcion;
    }

    public sealed class ManualExonerationResult
    {
        public string Authorization { get; set; } = string.Empty;
        public ExonerationModel Document { get; set; } = new();
    }

    public class ManualExonerationViewModel : INotifyPropertyChanged
    {
        public static readonly IReadOnlyList<ExonerationDocumentType> AllDocumentTypes = new List<ExonerationDocumentType>
        {
            new() { Codigo = "01", Descripcion = "Compras autorizadas por la Dirección General de Tributación" },
            new() { Codigo = "02", Descripcion = "Ventas exentas a diplomáticos" },
            new() { Codigo = "03", Descripcion = "Autorizados por Ley especial" },
            new() { Codigo = "04", Descripcion = "Exenciones Dirección General de Hacienda Autorización Local Genérica" },
            new() { Codigo = "05", Descripcion = "Exenciones Dirección General de Hacienda Transitorio V (servicios de ingeniería, arquitectura, topografía y construcción)" },
            new() { Codigo = "06", Descripcion = "Servicios turísticos inscritos ante el Instituto Costarricense de Turismo (ICT)" },
            new() { Codigo = "07", Descripcion = "Transitorio XVII (Recolección, Clasificación, almacenamiento de Reciclaje y reutilización)" },
            new() { Codigo = "08", Descripcion = "Exoneración a Zona Franca" },
            new() { Codigo = "09", Descripcion = "Exoneración de servicios complementarios para la exportación artículo 11 RLIVA" },
            new() { Codigo = "10", Descripcion = "Órgano de las corporaciones municipales" },
            new() { Codigo = "11", Descripcion = "Exenciones Dirección General de Hacienda Autorización de Impuesto Local Concreta" },
            new() { Codigo = "12", Descripcion = "Otros" },
        };

        private string _authorizationNumber = string.Empty;
        private ExonerationDocumentType? _selectedDocumentType;
        private DateTime _fechaEmision = DateTime.Today;
        private DateTime _fechaVencimiento = DateTime.Today;
        private string _institucion = string.Empty;
        private string _porcentajeText = string.Empty;
        private string _montoExoneradoText = "...";
        private bool _isBusy;
        private string _statusMessage = "* Llene la información y haga click en Aplicar para continuar...";
        private bool _isStatusSuccess;
        private decimal _cartSubtotalColones;
        private string _clientName = string.Empty;

        public IReadOnlyList<ExonerationDocumentType> DocumentTypes => AllDocumentTypes;

        public string AuthorizationNumber
        {
            get => _authorizationNumber;
            set
            {
                if (_authorizationNumber != value)
                {
                    _authorizationNumber = value;
                    OnPropertyChanged();
                    ((Command)BuscarCommand).ChangeCanExecute();
                }
            }
        }

        public ExonerationDocumentType? SelectedDocumentType
        {
            get => _selectedDocumentType;
            set { if (_selectedDocumentType != value) { _selectedDocumentType = value; OnPropertyChanged(); } }
        }

        public DateTime FechaEmision
        {
            get => _fechaEmision;
            set { if (_fechaEmision != value) { _fechaEmision = value; OnPropertyChanged(); } }
        }

        public DateTime FechaVencimiento
        {
            get => _fechaVencimiento;
            set { if (_fechaVencimiento != value) { _fechaVencimiento = value; OnPropertyChanged(); } }
        }

        public string Institucion
        {
            get => _institucion;
            set { if (_institucion != value) { _institucion = value; OnPropertyChanged(); } }
        }

        public string PorcentajeText
        {
            get => _porcentajeText;
            set { if (_porcentajeText != value) { _porcentajeText = value; OnPropertyChanged(); } }
        }

        public string MontoExoneradoText
        {
            get => _montoExoneradoText;
            private set { if (_montoExoneradoText != value) { _montoExoneradoText = value; OnPropertyChanged(); } }
        }

        public bool IsBusy
        {
            get => _isBusy;
            private set
            {
                if (_isBusy != value)
                {
                    _isBusy = value;
                    OnPropertyChanged();
                    ((Command)BuscarCommand).ChangeCanExecute();
                    ((Command)ApplyCommand).ChangeCanExecute();
                }
            }
        }

        public string StatusMessage
        {
            get => _statusMessage;
            private set { if (_statusMessage != value) { _statusMessage = value; OnPropertyChanged(); } }
        }

        public bool IsStatusSuccess
        {
            get => _isStatusSuccess;
            private set { if (_isStatusSuccess != value) { _isStatusSuccess = value; OnPropertyChanged(); } }
        }

        public event Action<string>? RequestBuscar;
        public event Action<ManualExonerationResult>? RequestApply;
        public event Action? RequestCancel;

        public ICommand BuscarCommand { get; }
        public ICommand CalcularMontoCommand { get; }
        public ICommand ApplyCommand { get; }
        public ICommand CancelCommand { get; }

        public ManualExonerationViewModel()
        {
            BuscarCommand = new Command(
                () => RequestBuscar?.Invoke(_authorizationNumber.Trim()),
                () => !_isBusy && !string.IsNullOrWhiteSpace(_authorizationNumber));
            CalcularMontoCommand = new Command(CalcularMonto);
            ApplyCommand = new Command(ExecuteApply, () => !_isBusy);
            CancelCommand = new Command(() => RequestCancel?.Invoke());
        }

        public void Load(string existingAuthorization, decimal cartSubtotalColones, string clientName = "")
        {
            _cartSubtotalColones = cartSubtotalColones;
            _clientName = clientName?.Trim() ?? string.Empty;
            AuthorizationNumber = existingAuthorization;
            SelectedDocumentType = AllDocumentTypes[0];
            FechaEmision = DateTime.Today;
            FechaVencimiento = DateTime.Today;
            Institucion = _clientName;
            PorcentajeText = string.Empty;
            MontoExoneradoText = "...";
            SetBusy(false);
            SetStatus("* Llene la información y haga click en Aplicar para continuar...", false);
        }

        public void SetBusy(bool busy) => IsBusy = busy;

        public void ApplyApiResult(ExonerationValidationResult result)
        {
            SetBusy(false);
            if (!result.IsValid || result.Document is null)
            {
                SetStatus(result.Message, false);
                return;
            }

            var doc = result.Document;
            AuthorizationNumber = doc.NumeroDocumento;
            SelectedDocumentType = AllDocumentTypes
                .FirstOrDefault(t => t.Codigo == doc.TipoDocumentoCodigo)
                ?? AllDocumentTypes[^1];
            FechaEmision = doc.FechaEmision ?? DateTime.Today;
            FechaVencimiento = doc.FechaVencimiento ?? DateTime.Today;
            Institucion = string.IsNullOrWhiteSpace(_clientName) ? doc.NombreInstitucion : _clientName;
            PorcentajeText = doc.PorcentajeExoneracion.ToString("0.##", CultureInfo.InvariantCulture);
            SetStatus("✓ Datos cargados desde Hacienda. Verifique y haga click en Aplicar.", true);
            CalcularMonto();
        }

        private void CalcularMonto()
        {
            if (decimal.TryParse(PorcentajeText, NumberStyles.Number, CultureInfo.InvariantCulture, out var pct)
                && pct > 0 && _cartSubtotalColones > 0)
                MontoExoneradoText = $"₡{_cartSubtotalColones * pct / 100m:N2}";
            else
                MontoExoneradoText = "...";
        }

        private void ExecuteApply()
        {
            if (!decimal.TryParse(PorcentajeText, NumberStyles.Number, CultureInfo.InvariantCulture, out var pct)
                && !decimal.TryParse(PorcentajeText, NumberStyles.Number, CultureInfo.CurrentCulture, out pct))
            {
                SetStatus("Ingrese un porcentaje válido (ej. 13).", false);
                return;
            }

            if (pct <= 0 || pct > 100)
            {
                SetStatus("El porcentaje debe estar entre 1 y 100.", false);
                return;
            }

            if (_selectedDocumentType is null)
            {
                SetStatus("Seleccione el tipo de documento.", false);
                return;
            }

            var document = new ExonerationModel
            {
                NumeroDocumento = string.IsNullOrWhiteSpace(_authorizationNumber) ? "MANUAL" : _authorizationNumber.Trim(),
                PorcentajeExoneracion = pct,
                FechaEmision = _fechaEmision,
                FechaVencimiento = _fechaVencimiento,
                NombreInstitucion = string.IsNullOrWhiteSpace(_institucion) ? "Ingreso manual" : _institucion.Trim(),
                TipoDocumentoCodigo = _selectedDocumentType.Codigo,
                TipoDocumentoDescripcion = _selectedDocumentType.Descripcion,
                PoseeCabys = false
            };

            RequestApply?.Invoke(new ManualExonerationResult
            {
                Authorization = document.NumeroDocumento,
                Document = document
            });
        }

        private void SetStatus(string message, bool isSuccess)
        {
            StatusMessage = message;
            IsStatusSuccess = isSuccess;
        }

        public event PropertyChangedEventHandler? PropertyChanged;
        protected void OnPropertyChanged([CallerMemberName] string? name = null)
            => PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(name));
    }
}
