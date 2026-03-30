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
        private decimal _exonerationPercent;
        private bool _hasExonerationEligibility;
        private bool _isExonerationEligible;
        private string _discountReasonCode = string.Empty;
        private int _discountReasonCodeID;
        private int _exonerationReasonCodeID;
        private int _salesRepID;
        private string _salesRepName = string.Empty;

        public int ItemID { get; set; }
        public string Emoji { get; set; } = string.Empty;
        public string Name { get; set; } = string.Empty;
        public string Code { get; set; } = string.Empty;
        public decimal UnitPrice { get; set; }
        public decimal UnitPriceColones { get; set; }
        public decimal TaxPercentage { get; set; }
        public int TaxID { get; set; }
        public string Cabys { get; set; } = string.Empty;
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
                    OnPropertyChanged(nameof(IsUpwardPriceOverride));
                    OnPropertyChanged(nameof(HasDownwardPriceOverride));
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

        public int DiscountReasonCodeID
        {
            get => _discountReasonCodeID;
            set { _discountReasonCodeID = value; OnPropertyChanged(); }
        }

        public int ExonerationReasonCodeID
        {
            get => _exonerationReasonCodeID;
            set { _exonerationReasonCodeID = value; OnPropertyChanged(); }
        }

        public decimal ExonerationPercent
        {
            get => _exonerationPercent;
            set
            {
                var normalized = Math.Clamp(value, 0m, 100m);
                if (_exonerationPercent != normalized)
                {
                    _exonerationPercent = normalized;
                    OnPropertyChanged();
                    OnPropertyChanged(nameof(EffectiveTaxPercentage));
                    OnPropertyChanged(nameof(HasExoneration));
                    OnPropertyChanged(nameof(ExonerationText));
                    OnPropertyChanged(nameof(CanShowApplyExoneration));
                    OnPropertyChanged(nameof(IsModified));
                }
            }
        }

        public bool HasExonerationEligibility
        {
            get => _hasExonerationEligibility;
            set
            {
                if (_hasExonerationEligibility != value)
                {
                    _hasExonerationEligibility = value;
                    OnPropertyChanged();
                    OnPropertyChanged(nameof(ExonerationEligibilityText));
                    OnPropertyChanged(nameof(CanShowApplyExoneration));
                }
            }
        }

        public bool IsExonerationEligible
        {
            get => _isExonerationEligible;
            set
            {
                if (_isExonerationEligible != value)
                {
                    _isExonerationEligible = value;
                    OnPropertyChanged();
                    OnPropertyChanged(nameof(ExonerationEligibilityText));
                    OnPropertyChanged(nameof(CanShowApplyExoneration));
                }
            }
        }

        public int SalesRepID
        {
            get => _salesRepID;
            set { if (_salesRepID != value) { _salesRepID = value; OnPropertyChanged(); OnPropertyChanged(nameof(HasSalesRep)); OnPropertyChanged(nameof(SalesRepIndicator)); } }
        }

        public string SalesRepName
        {
            get => _salesRepName;
            set { if (_salesRepName != value) { _salesRepName = value ?? string.Empty; OnPropertyChanged(); OnPropertyChanged(nameof(HasSalesRep)); OnPropertyChanged(nameof(SalesRepIndicator)); } }
        }

        public bool HasSalesRep => _salesRepID > 0;
        public string SalesRepIndicator => HasSalesRep ? $"🧑‍💼 {_salesRepName}" : string.Empty;

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
        public bool IsUpwardPriceOverride => _overridePriceColones.HasValue && _overridePriceColones.Value > UnitPriceColones;
        public bool HasDownwardPriceOverride => _overridePriceColones.HasValue && _overridePriceColones.Value < UnitPriceColones;
        public bool HasDiscount => _discountPercent > 0;
        public bool HasExoneration => _exonerationPercent > 0;
        public bool CanShowApplyExoneration => !HasExoneration && HasExonerationEligibility && IsExonerationEligible;
        public decimal EffectiveTaxPercentage => Math.Max(0m, TaxPercentage - _exonerationPercent);
        public bool IsModified => HasOverridePrice || HasDiscount || HasExoneration;
        public string DiscountText => HasDiscount ? $"-{_discountPercent:F0}%" : string.Empty;
        public string ExonerationText => HasExoneration ? $"Exon. {Math.Min(TaxPercentage, _exonerationPercent):0.##}%" : string.Empty;
        public string ExonerationEligibilityText => HasExonerationEligibility
            ? (IsExonerationEligible ? "Exonerable" : "No exonerable")
            : string.Empty;
        public string PriceOverrideIndicator => _overridePriceColones.HasValue ? "✱ " : string.Empty;
        private decimal DiscountFactor => 1m - _discountPercent / 100m;
        private decimal EffectiveUnitPriceUsd => UnitPriceColones > 0
            ? Math.Round(EffectivePriceColones * UnitPrice / UnitPriceColones, 2)
            : UnitPrice;

        public string QuantityText => $"{Quantity:0.##} ×";
        public string QuantityPrefix => $"x{Quantity:0.##}";
        public string UnitPriceColonesText => $"{UiConfig.CurrencySymbol}{EffectivePriceColones:N2}";
        public string OriginalPriceColonesText => $"{UiConfig.CurrencySymbol}{UnitPriceColones:N2}";
        public string TotalColonesText => $"{UiConfig.CurrencySymbol}{EffectivePriceColones * Quantity * DiscountFactor:N2}";
        public string UnitPriceUsdText => $"${UnitPrice:F2}";
        public string TotalUsdText => $"${EffectiveUnitPriceUsd * Quantity * DiscountFactor:F2}";

        public event PropertyChangedEventHandler? PropertyChanged;
        protected void OnPropertyChanged([CallerMemberName] string? name = null)
            => PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(name));
    }
}
