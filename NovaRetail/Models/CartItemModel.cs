using System.ComponentModel;
using System.Runtime.CompilerServices;

namespace NovaRetail.Models
{
    public class CartItemModel : INotifyPropertyChanged
    {
        private decimal _quantity = 1m;
        private decimal? _overridePriceColones;
        private string? _overrideDescription;
        private decimal _discountPercent;
        private string _discountReasonCode = string.Empty;

        public string Emoji { get; set; } = string.Empty;
        public string Name { get; set; } = string.Empty;
        public string Code { get; set; } = string.Empty;
        public decimal UnitPrice { get; set; }
        public decimal UnitPriceColones { get; set; }
        public decimal Stock { get; set; }

        public decimal? OverridePriceColones
        {
            get => _overridePriceColones;
            set
            {
                if (_overridePriceColones != value)
                {
                    _overridePriceColones = value;
                    OnPropertyChanged();
                    OnPropertyChanged(nameof(EffectivePriceColones));
                    OnPropertyChanged(nameof(UnitPriceColonesText));
                    OnPropertyChanged(nameof(TotalColonesText));
                    OnPropertyChanged(nameof(TotalUsdText));
                    OnPropertyChanged(nameof(HasOverridePrice));
                    OnPropertyChanged(nameof(PriceOverrideIndicator));
                    OnPropertyChanged(nameof(IsModified));
                }
            }
        }

        public string? OverrideDescription
        {
            get => _overrideDescription;
            set
            {
                if (_overrideDescription != value)
                {
                    _overrideDescription = value;
                    OnPropertyChanged();
                    OnPropertyChanged(nameof(DisplayName));
                }
            }
        }

        public decimal DiscountPercent
        {
            get => _discountPercent;
            set
            {
                if (_discountPercent != value)
                {
                    _discountPercent = Math.Clamp(value, 0, 100);
                    OnPropertyChanged();
                    OnPropertyChanged(nameof(TotalColonesText));
                    OnPropertyChanged(nameof(TotalUsdText));
                    OnPropertyChanged(nameof(DiscountText));
                    OnPropertyChanged(nameof(HasDiscount));
                    OnPropertyChanged(nameof(IsModified));
                }
            }
        }

        public string DiscountReasonCode
        {
            get => _discountReasonCode;
            set { _discountReasonCode = value ?? string.Empty; OnPropertyChanged(); }
        }

        private bool _isSelected;
        public bool IsSelected
        {
            get => _isSelected;
            set { if (_isSelected != value) { _isSelected = value; OnPropertyChanged(); } }
        }

        public decimal Quantity
        {
            get => _quantity;
            set
            {
                if (_quantity != value)
                {
                    _quantity = value;
                    OnPropertyChanged();
                    OnPropertyChanged(nameof(QuantityText));
                    OnPropertyChanged(nameof(QuantityPrefix));
                    OnPropertyChanged(nameof(TotalColonesText));
                    OnPropertyChanged(nameof(TotalUsdText));
                }
            }
        }

        public decimal EffectivePriceColones => _overridePriceColones ?? UnitPriceColones;
        public string DisplayName => _overrideDescription ?? Name;
        public bool HasOverridePrice => _overridePriceColones.HasValue;
        public bool HasDiscount => _discountPercent > 0;
        public bool IsModified => HasOverridePrice || HasDiscount;
        public string DiscountText => HasDiscount ? $"-{_discountPercent:F0}%" : string.Empty;
        public string PriceOverrideIndicator => _overridePriceColones.HasValue ? "✱ " : string.Empty;
        private decimal DiscountFactor => 1m - _discountPercent / 100m;
        private decimal EffectiveUnitPriceUsd => UnitPriceColones > 0
            ? Math.Round(EffectivePriceColones * UnitPrice / UnitPriceColones, 2)
            : UnitPrice;

        public string QuantityText => $"{Quantity:0.##} ×";
        public string QuantityPrefix => $"x{Quantity:0.##}";
        public string UnitPriceColonesText => $"₡{EffectivePriceColones:N2}";
        public string OriginalPriceColonesText => $"₡{UnitPriceColones:N2}";
        public string TotalColonesText => $"₡{EffectivePriceColones * Quantity * DiscountFactor:N2}";
        public string UnitPriceUsdText => $"${UnitPrice:F2}";
        public string TotalUsdText => $"${EffectiveUnitPriceUsd * Quantity * DiscountFactor:F2}";

        public event PropertyChangedEventHandler? PropertyChanged;
        protected void OnPropertyChanged([CallerMemberName] string? name = null)
            => PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(name));
    }
}
