using System.ComponentModel;
using System.Runtime.CompilerServices;

namespace NovaRetail.Models
{
    public class ProductModel : INotifyPropertyChanged
    {
        private int _cartQuantity;

        public string  Emoji           { get; set; } = string.Empty;
        public string  Name            { get; set; } = string.Empty;
        public string  Code            { get; set; } = string.Empty;
        public string  Price           { get; set; } = string.Empty;
        public string  OldPrice        { get; set; } = string.Empty;
        public decimal PriceValue      { get; set; }
        public decimal PriceColonesValue { get; set; }
        public string  PriceColonesText => $"₡{PriceColonesValue:N0}";
        public string  Category        { get; set; } = string.Empty;
        public int     Stock           { get; set; }

        public int CartQuantity
        {
            get => _cartQuantity;
            set
            {
                if (_cartQuantity != value)
                {
                    _cartQuantity = value;
                    OnPropertyChanged();
                }
            }
        }

        public event PropertyChangedEventHandler? PropertyChanged;
        protected void OnPropertyChanged([CallerMemberName] string? name = null)
            => PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(name));
    }
}
