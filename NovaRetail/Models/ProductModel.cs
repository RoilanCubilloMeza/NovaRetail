using System.ComponentModel;
using System.Runtime.CompilerServices;

namespace NovaRetail.Models
{
    public class ProductModel : INotifyPropertyChanged
    {
        private decimal _cartQuantity;

        public int     ItemID          { get; set; }
        public string  Emoji           { get; set; } = string.Empty;
        public string  Name            { get; set; } = string.Empty;
        public string  Code            { get; set; } = string.Empty;
        public string  Price           { get; set; } = string.Empty;
        public string  OldPrice        { get; set; } = string.Empty;
        public decimal PriceValue      { get; set; }
        public decimal PriceColonesValue { get; set; }
        public decimal TaxPercentage   { get; set; }
        public int TaxId              { get; set; }
        public string Cabys           { get; set; } = string.Empty;
        public string  PriceColonesText => $"₡{PriceColonesValue:N2}";
        public string  Category        { get; set; } = string.Empty;
        public decimal     Stock           { get; set; }

        public decimal CartQuantity
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
