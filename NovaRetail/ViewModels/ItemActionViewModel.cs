using NovaRetail.Models;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Globalization;
using System.Runtime.CompilerServices;
using System.Windows.Input;

namespace NovaRetail.ViewModels
{
    public class ItemActionViewModel : INotifyPropertyChanged
    {
        private CartItemModel? _item;
        private decimal _originalPrice;
        private decimal _maxStock = int.MaxValue;
        private string _activeField = "Cantidad";
        private string _inputBuffer = string.Empty;

        private string _tempQty = "1";
        private string _tempPrice = "0";
        private string _tempExtPrice = "0";
        private string _tempDesc = string.Empty;

        private string _itemName = string.Empty;
        private string _itemCode = string.Empty;
        private string _precioText = string.Empty;
        private string _disponibleText = "N/D";
        private string _comprometidoText = "0";

        private bool _isDiscountVisible;
        private bool _isDiscountConfirmVisible;
        private ReasonCodeModel? _selectedDiscountCode;
        private string _discountPercentText = "0";

        // ── Product info (read-only) ──

        public string ItemName
        {
            get => _itemName;
            private set { _itemName = value; OnPropertyChanged(); }
        }
        public string ItemCode
        {
            get => _itemCode;
            private set { _itemCode = value; OnPropertyChanged(); }
        }
        public string PrecioText
        {
            get => _precioText;
            private set { _precioText = value; OnPropertyChanged(); }
        }
        public string DisponibleText
        {
            get => _disponibleText;
            private set { _disponibleText = value; OnPropertyChanged(); }
        }
        public string ComprometidoText
        {
            get => _comprometidoText;
            private set { _comprometidoText = value; OnPropertyChanged(); }
        }

        // ── Editable field texts ──

        public string TempQty
        {
            get => _tempQty;
            private set
            {
                if (_tempQty != value)
                {
                    _tempQty = value;
                    OnPropertyChanged();
                    OnPropertyChanged(nameof(IsAtMaxStock));
                }
            }
        }
        public string TempPrice
        {
            get => _tempPrice;
            private set { if (_tempPrice != value) { _tempPrice = value; OnPropertyChanged(); } }
        }
        public string TempExtPrice
        {
            get => _tempExtPrice;
            private set { if (_tempExtPrice != value) { _tempExtPrice = value; OnPropertyChanged(); } }
        }
        public string TempDesc
        {
            get => _tempDesc;
            private set { if (_tempDesc != value) { _tempDesc = value; OnPropertyChanged(); } }
        }

        public bool IsAtMaxStock =>
            _maxStock < int.MaxValue &&
            decimal.TryParse(_tempQty, NumberStyles.Any, CultureInfo.InvariantCulture, out var q) &&
            q >= _maxStock;

        // ── Active field indicators ──

        public bool IsCantidadActive => _activeField == "Cantidad";
        public bool IsPrecioActive => _activeField == "Precio";
        public bool IsPrecioExtActive => _activeField == "PrecioExt";
        public bool IsDescripcionActive => _activeField == "Descripcion";
        public bool IsDescuentoPctActive => _activeField == "DescuentoPct";

        // ── Discount panel ──

        public bool IsDiscountVisible
        {
            get => _isDiscountVisible;
            private set { if (_isDiscountVisible != value) { _isDiscountVisible = value; OnPropertyChanged(); } }
        }
        public bool IsDiscountConfirmVisible
        {
            get => _isDiscountConfirmVisible;
            private set { if (_isDiscountConfirmVisible != value) { _isDiscountConfirmVisible = value; OnPropertyChanged(); } }
        }
        public ReasonCodeModel? SelectedDiscountCode
        {
            get => _selectedDiscountCode;
            private set { _selectedDiscountCode = value; OnPropertyChanged(); OnPropertyChanged(nameof(SelectedDiscountText)); }
        }
        public string SelectedDiscountText => _selectedDiscountCode?.DisplayText ?? "—";
        public string DiscountPercentText
        {
            get => _discountPercentText;
            private set { if (_discountPercentText != value) { _discountPercentText = value; OnPropertyChanged(); } }
        }

        public ObservableCollection<ReasonCodeModel> DiscountCodes { get; } = new();

        public CartItemModel? CurrentItem => _item;

        // ── Events ──

        public event Action? RequestOk;
        public event Action? RequestCancel;
        public event Action? RequestPriceJustification;
        public event Action? RequestItemDiscount;

        public decimal? PendingPriceColones =>
            decimal.TryParse(_tempPrice, System.Globalization.NumberStyles.Any,
                System.Globalization.CultureInfo.InvariantCulture, out var p) && p > 0 ? p : null;

        // ── Commands ──

        public ICommand KeypadCommand { get; }
        public ICommand SetActiveFieldCommand { get; }
        public ICommand OkCommand { get; }
        public ICommand CancelCommand { get; }
        public ICommand ShowDiscountCommand { get; }
        public ICommand SelectDiscountCommand { get; }
        public ICommand ConfirmDiscountCommand { get; }
        public ICommand IncrQtyCommand { get; }
        public ICommand DecrQtyCommand { get; }

        public ItemActionViewModel()
        {
            KeypadCommand = new Command<string>(HandleKeypad);
            SetActiveFieldCommand = new Command<string>(SetActiveField);
            OkCommand = new Command(() =>
            {
                CommitCurrentBuffer();
                var currentPrice = decimal.TryParse(_tempPrice, System.Globalization.NumberStyles.Any,
                    System.Globalization.CultureInfo.InvariantCulture, out var p) ? p : _originalPrice;

                if (_originalPrice > 0 && Math.Abs(currentPrice - _originalPrice) > 0.001m)
                {
                    ApplyNonPriceChanges();
                    RequestPriceJustification?.Invoke();
                }
                else
                {
                    ApplyChanges();
                    RequestOk?.Invoke();
                }
            });
            CancelCommand = new Command(() => RequestCancel?.Invoke());
            ShowDiscountCommand = new Command(() => RequestItemDiscount?.Invoke());
            SelectDiscountCommand = new Command<ReasonCodeModel>(SelectDiscount);
            ConfirmDiscountCommand = new Command(ConfirmDiscount);
            IncrQtyCommand = new Command(() => AdjustQty(1));
            DecrQtyCommand = new Command(() => AdjustQty(-1));
        }

        public void LoadItem(CartItemModel item, IEnumerable<ReasonCodeModel> discountCodes)
        {
            _item = item;
            _originalPrice = item.EffectivePriceColones;
            _maxStock = item.Stock > 0 ? item.Stock : int.MaxValue;
            ItemName = item.OverrideDescription ?? item.Name;
            ItemCode = item.Code;

            var ep = item.EffectivePriceColones;
            PrecioText = $"₡{ep:N2}";
            DisponibleText = item.Stock > 0 ? item.Stock.ToString("0.##") : "N/D";
            ComprometidoText = "0";

            _tempQty = item.Quantity.ToString("0.##", CultureInfo.InvariantCulture);
            _tempPrice = ep.ToString("0.##", CultureInfo.InvariantCulture);
            _tempExtPrice = (ep * item.Quantity).ToString("0.##", CultureInfo.InvariantCulture);
            _tempDesc = item.OverrideDescription ?? item.Name;
            _discountPercentText = item.DiscountPercent > 0 ? item.DiscountPercent.ToString("F0", CultureInfo.InvariantCulture) : "0";
            _selectedDiscountCode = null;

            _activeField = "Precio";
            _inputBuffer = _tempPrice;

            IsDiscountVisible = false;
            IsDiscountConfirmVisible = false;

            DiscountCodes.Clear();
            foreach (var dc in discountCodes)
                DiscountCodes.Add(dc);

            OnPropertyChanged(string.Empty);
        }

        // ── Discount panel ──

        private void ToggleDiscountPanel()
        {
            if (IsDiscountConfirmVisible)
            {
                IsDiscountConfirmVisible = false;
                IsDiscountVisible = false;
            }
            else
            {
                IsDiscountVisible = !IsDiscountVisible;
                if (!IsDiscountVisible)
                    IsDiscountConfirmVisible = false;
            }
        }

        private void SelectDiscount(ReasonCodeModel? code)
        {
            if (code is null) return;
            SelectedDiscountCode = code;
            IsDiscountVisible = false;
            IsDiscountConfirmVisible = true;
            _activeField = "DescuentoPct";
            _inputBuffer = _discountPercentText;
            BroadcastActiveField();
        }

        private void ConfirmDiscount()
        {
            if (_item is null) return;
            if (decimal.TryParse(_discountPercentText, NumberStyles.Any, CultureInfo.InvariantCulture, out var pct))
            {
                _item.DiscountPercent = Math.Clamp(pct, 0, 100);
                _item.DiscountReasonCode = SelectedDiscountCode?.Code ?? string.Empty;
            }
            IsDiscountConfirmVisible = false;
        }

        // ── Active field management ──

        private void SetActiveField(string field)
        {
            CommitCurrentBuffer();
            _activeField = field;
            _inputBuffer = GetCurrentFieldValue();
            BroadcastActiveField();
        }

        // ── Keypad handler ──

        private void HandleKeypad(string key)
        {
            switch (key)
            {
                case "C":
                    _inputBuffer = string.Empty;
                    break;
                case "Regresar":
                    if (_inputBuffer.Length > 0)
                        _inputBuffer = _inputBuffer[..^1];
                    break;
                case "ENT":
                    CommitCurrentBuffer();
                    if (_activeField == "DescuentoPct")
                        ConfirmDiscount();
                    else
                        NavigateNext();
                    return;
                case ".":
                    if (!_inputBuffer.Contains('.'))
                        _inputBuffer += ".";
                    break;
                default:
                    if (key.Length == 1 && char.IsDigit(key[0]))
                        _inputBuffer += key;
                    break;
            }
            UpdateFieldDisplay();
        }

        private void CommitCurrentBuffer()
        {
            if (string.IsNullOrEmpty(_inputBuffer)) return;

            switch (_activeField)
            {
                case "Cantidad":
                    if (decimal.TryParse(_inputBuffer, NumberStyles.Any, CultureInfo.InvariantCulture, out var qty) && qty > 0)
                    {
                        TempQty = Math.Min(qty, _maxStock).ToString("0.##", CultureInfo.InvariantCulture);
                        RecalcExtPrice();
                    }
                    break;
                case "Precio":
                    if (decimal.TryParse(_inputBuffer, NumberStyles.Any, CultureInfo.InvariantCulture, out var price) && price >= 0)
                    {
                        TempPrice = price.ToString("G", CultureInfo.InvariantCulture);
                        RecalcExtPrice();
                    }
                    break;
                case "PrecioExt":
                    if (decimal.TryParse(_inputBuffer, NumberStyles.Any, CultureInfo.InvariantCulture, out var ext) && ext >= 0)
                    {
                        TempExtPrice = ext.ToString("G", CultureInfo.InvariantCulture);
                        if (decimal.TryParse(TempQty, NumberStyles.Any, CultureInfo.InvariantCulture, out var q) && q > 0)
                            TempPrice = (ext / q).ToString("G", CultureInfo.InvariantCulture);
                    }
                    break;
                case "Descripcion":
                    TempDesc = _inputBuffer;
                    break;
                case "DescuentoPct":
                    if (decimal.TryParse(_inputBuffer, NumberStyles.Any, CultureInfo.InvariantCulture, out var pct)
                        && pct >= 0 && pct <= 100)
                        DiscountPercentText = ((int)pct).ToString();
                    break;
            }
        }

        private void UpdateFieldDisplay()
        {
            switch (_activeField)
            {
                case "Cantidad": TempQty = _inputBuffer; break;
                case "Precio": TempPrice = _inputBuffer; break;
                case "PrecioExt": TempExtPrice = _inputBuffer; break;
                case "Descripcion": TempDesc = _inputBuffer; break;
                case "DescuentoPct": DiscountPercentText = _inputBuffer; break;
            }
        }

        private void RecalcExtPrice()
        {
            if (decimal.TryParse(TempQty, NumberStyles.Any, CultureInfo.InvariantCulture, out var q) &&
                decimal.TryParse(TempPrice, NumberStyles.Any, CultureInfo.InvariantCulture, out var p))
                TempExtPrice = (q * p).ToString("G", CultureInfo.InvariantCulture);
        }

        private void AdjustQty(int delta)
        {
            CommitCurrentBuffer();
            if (!decimal.TryParse(TempQty, NumberStyles.Any, CultureInfo.InvariantCulture, out var q)) q = 1;
            q = Math.Clamp(q + delta, 1m, _maxStock);
            TempQty = q.ToString("0.##", CultureInfo.InvariantCulture);
            _inputBuffer = TempQty;
            RecalcExtPrice();
        }

        private void AdjustDiscountPct(int delta)
        {
            CommitCurrentBuffer();
            if (!decimal.TryParse(DiscountPercentText, NumberStyles.Any, CultureInfo.InvariantCulture, out var p)) p = 0;
            p = Math.Clamp(p + delta, 0, 100);
            DiscountPercentText = ((int)p).ToString();
            _inputBuffer = DiscountPercentText;
        }

        // ── Field navigation ──

        private void NavigateNext()
        {
            _activeField = _activeField switch
            {
                "Cantidad" => "Precio",
                _ => "Cantidad"
            };
            _inputBuffer = GetCurrentFieldValue();
            BroadcastActiveField();
        }

        private void NavigatePrev()
        {
            _activeField = _activeField switch
            {
                "Precio" => "Cantidad",
                _ => "Precio"
            };
            _inputBuffer = GetCurrentFieldValue();
            BroadcastActiveField();
        }

        private string GetCurrentFieldValue() => _activeField switch
        {
            "Cantidad" => TempQty,
            "Precio" => TempPrice,
            "PrecioExt" => TempExtPrice,
            "Descripcion" => TempDesc,
            "DescuentoPct" => DiscountPercentText,
            _ => string.Empty
        };

        private void BroadcastActiveField()
        {
            OnPropertyChanged(nameof(IsCantidadActive));
            OnPropertyChanged(nameof(IsPrecioActive));
            OnPropertyChanged(nameof(IsPrecioExtActive));
            OnPropertyChanged(nameof(IsDescripcionActive));
            OnPropertyChanged(nameof(IsDescuentoPctActive));
        }

        // ── Apply to item ──

        private void ApplyChanges()
        {
            if (_item is null) return;
            CommitCurrentBuffer();

            if (decimal.TryParse(TempQty, NumberStyles.Any, CultureInfo.InvariantCulture, out var qty) && qty > 0)
                _item.Quantity = qty;

            if (decimal.TryParse(TempPrice, NumberStyles.Any, CultureInfo.InvariantCulture, out var price) && price > 0)
                _item.OverridePriceColones = price;
            else
                _item.OverridePriceColones = null;

            var trimDesc = TempDesc.Trim();
            _item.OverrideDescription = string.IsNullOrWhiteSpace(trimDesc) || trimDesc == _item.Name
                ? null : trimDesc;
        }

        // Applies quantity + description changes but NOT the price (price requires justification)
        public void ApplyNonPriceChanges()
        {
            if (_item is null) return;

            if (decimal.TryParse(TempQty, NumberStyles.Any, CultureInfo.InvariantCulture, out var qty) && qty > 0)
                _item.Quantity = qty;

            var trimDesc = TempDesc.Trim();
            _item.OverrideDescription = string.IsNullOrWhiteSpace(trimDesc) || trimDesc == _item.Name
                ? null : trimDesc;
        }

        public event PropertyChangedEventHandler? PropertyChanged;
        protected void OnPropertyChanged([CallerMemberName] string? name = null)
            => PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(name));
    }
}
