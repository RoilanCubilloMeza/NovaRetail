using System.ComponentModel;
using System.Runtime.CompilerServices;

namespace NovaRetail.Models;

public class ProductModel : INotifyPropertyChanged
{
    private decimal _cartQuantity;

    public int     ItemID          { get; set; }
    public int     DepartmentID    { get; set; }
    public string  Emoji           { get; set; } = string.Empty;
    public string  Name            { get; set; } = string.Empty;
    public string  Code            { get; set; } = string.Empty;
    public string  Price           { get; set; } = string.Empty;
    public string  OldPrice        { get; set; } = string.Empty;
    public decimal PriceValue      { get; set; }
    public decimal PriceColonesValue { get; set; }
    public decimal Cost            { get; set; }
    public decimal TaxPercentage   { get; set; }
    public int TaxId              { get; set; }
    public string Cabys           { get; set; } = string.Empty;
    public string  PriceColonesText => $"{UiConfig.CurrencySymbol}{PriceColonesValue:N2}";
    public string  Category        { get; set; } = string.Empty;
    public decimal     Stock           { get; set; }
    public int     ItemType        { get; set; }
    public bool    IsNonInventory  { get; set; }
    public string DepartmentDisplayName => string.IsNullOrWhiteSpace(Category) ? "Sin categoría" : Category;
    public string CategoryDisplayName => string.IsNullOrWhiteSpace(Category) ? "Sin categoría" : Category;
    public string TypeDisplayName => IsNonInventory ? "Servicio" : "Estándar";
    public bool IsOutOfStock => !IsNonInventory && Stock <= 0;
    public bool IsLowStock => !IsNonInventory && Stock > 0 && Stock <= 4;
    public string StockDisplayText => IsNonInventory
        ? "Servicio"
        : IsOutOfStock
            ? "Agotado"
            : IsLowStock
                ? $"Bajo ({Stock:0.###})"
                : $"{Stock:0.###} disp.";
    public string CartQuantityText => HasCartQuantity ? $"{CartQuantity:0.###} en carrito" : "Listo para agregar";
    public bool HasCartQuantity => CartQuantity > 0;
    public string CabysDisplayText => string.IsNullOrWhiteSpace(Cabys) ? "Sin CABYS" : $"CABYS {Cabys}";

    public decimal CartQuantity
    {
        get => _cartQuantity;
        set
        {
            if (_cartQuantity != value)
            {
                _cartQuantity = value;
                OnPropertyChanged();
                OnPropertyChanged(nameof(HasCartQuantity));
                OnPropertyChanged(nameof(CartQuantityText));
            }
        }
    }

    public event PropertyChangedEventHandler? PropertyChanged;
    protected void OnPropertyChanged([CallerMemberName] string? name = null)
        => PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(name));
}
