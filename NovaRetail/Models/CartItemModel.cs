using System.ComponentModel;
using System.Runtime.CompilerServices;

namespace NovaRetail.Models
{
    public class CartItemModel : INotifyPropertyChanged
    {
        private int _quantity = 1;

        public string Emoji { get; set; } = string.Empty;
        public string Name { get; set; } = string.Empty;
        public string Code { get; set; } = string.Empty;
        public decimal UnitPrice { get; set; }
        public decimal UnitPriceColones { get; set; }

        public int Quantity
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

        public string QuantityText => $"{Quantity} ×";
        public string QuantityPrefix => $"x{Quantity}";
        public string UnitPriceColonesText => $"₡{UnitPriceColones:N0}";
        public string TotalColonesText => $"₡{UnitPriceColones * Quantity:N0}";
        public string UnitPriceUsdText => $"${UnitPrice:F2}";
        public string TotalUsdText => $"${UnitPrice * Quantity:F2}";

        public event PropertyChangedEventHandler? PropertyChanged;
        protected void OnPropertyChanged([CallerMemberName] string? name = null)
            => PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(name));
    }
}
