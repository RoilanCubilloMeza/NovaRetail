using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Runtime.CompilerServices;
using System.Windows.Input;
using NovaRetail.Models;
using NovaRetail.Services;

namespace NovaRetail.ViewModels
{
    public class MainViewModel : INotifyPropertyChanged
    {
        private readonly IDialogService _dialogService;
        private readonly List<ProductModel> _allProducts = new();

        public ObservableCollection<ProductModel> Products { get; } = new();
        public ObservableCollection<CartItemModel> CartItems { get; } = new();

        public ICommand AddProductCommand { get; }
        public ICommand IncrementCommand { get; }
        public ICommand DecrementCommand { get; }
        public ICommand ClearCartCommand { get; }
        public ICommand InvoiceCommand { get; }
        public ICommand SearchProductCommand { get; }
        public ICommand SelectCategoryCommand { get; }
        public ICommand SelectTabCommand { get; }
        public ICommand ApplyDiscountCommand { get; }
        public ICommand ToggleProductsPanelCommand { get; }
        public ICommand DecrementProductCommand { get; }
        public ICommand SelectSpanCommand { get; }
        public ICommand NavigateToClienteCommand { get; }

        private decimal _subtotal;
        public decimal Subtotal
        {
            get => _subtotal;
            private set
            {
                if (_subtotal != value)
                {
                    _subtotal = value;
                    OnPropertyChanged();
                    OnPropertyChanged(nameof(TaxText));
                    OnPropertyChanged(nameof(TotalText));
                    OnPropertyChanged(nameof(DiscountAmountText));
                }
            }
        }

        private string _productSearchText = string.Empty;
        public string ProductSearchText
        {
            get => _productSearchText;
            set
            {
                if (_productSearchText != value)
                {
                    _productSearchText = value;
                    OnPropertyChanged();
                    FilterProducts();
                }
            }
        }

        // ── Tab del panel izquierdo: Rápido / Categorías / Promos ──

        private string _selectedTab = "Rápido";
        public string SelectedTab
        {
            get => _selectedTab;
            set
            {
                if (_selectedTab != value)
                {
                    _selectedTab = value;
                    OnPropertyChanged();
                    OnPropertyChanged(nameof(IsTabRapido));
                    OnPropertyChanged(nameof(IsTabCategorias));
                    OnPropertyChanged(nameof(IsTabPromos));
                    OnPropertyChanged(nameof(ShowCategoryTabs));
                    OnPropertyChanged(nameof(BreadcrumbText));

                    if (value == "Rápido" || value == "Promos")
                        SelectedCategory = "Todos";

                    FilterProducts();
                }
            }
        }

        public bool IsTabRapido => SelectedTab == "Rápido";
        public bool IsTabCategorias => SelectedTab == "Categorías";
        public bool IsTabPromos => SelectedTab == "Promos";
        public bool ShowCategoryTabs => SelectedTab == "Categorías";

        // ── Categoría del panel central ──

        private string _selectedCategory = "Todos";
        public string SelectedCategory
        {
            get => _selectedCategory;
            set
            {
                if (_selectedCategory != value)
                {
                    _selectedCategory = value;
                    OnPropertyChanged();
                    OnPropertyChanged(nameof(IsCatTodos));
                    OnPropertyChanged(nameof(IsCatSuper));
                    OnPropertyChanged(nameof(IsCatFerreteria));
                    OnPropertyChanged(nameof(IsCatCalzado));
                    OnPropertyChanged(nameof(IsCatHogar));
                    OnPropertyChanged(nameof(BreadcrumbText));
                    FilterProducts();
                }
            }
        }

        public bool IsCatTodos => SelectedCategory == "Todos";
        public bool IsCatSuper => SelectedCategory == "Super";
        public bool IsCatFerreteria => SelectedCategory == "Ferreteria";
        public bool IsCatCalzado => SelectedCategory == "Calzado";
        public bool IsCatHogar => SelectedCategory == "Hogar";

        public string BreadcrumbText
        {
            get
            {
                if (SelectedTab == "Promos")
                    return "🏷️  Promociones activas";
                if (SelectedTab == "Categorías" && SelectedCategory != "Todos")
                    return $"📋  Categorías  /  {SelectedCategory}";
                return "📋  Todos los productos";
            }
        }

        // ── Descuento ──

        private int _discountPercent;
        public int DiscountPercent
        {
            get => _discountPercent;
            set
            {
                if (_discountPercent != value)
                {
                    _discountPercent = Math.Clamp(value, 0, 100);
                    OnPropertyChanged();
                    OnPropertyChanged(nameof(DiscountText));
                    OnPropertyChanged(nameof(DiscountAmountText));
                    OnPropertyChanged(nameof(TaxText));
                    OnPropertyChanged(nameof(TotalText));
                }
            }
        }

        public string DiscountText => $"{DiscountPercent} %";
        private decimal DiscountAmount => Math.Round(Subtotal * DiscountPercent / 100m, 2);
        public string DiscountAmountText => $"-${DiscountAmount:F2}";
        private decimal SubtotalAfterDiscount => Subtotal - DiscountAmount;
        public decimal Tax => Math.Round(SubtotalAfterDiscount * 0.055m, 2);
        public string TaxText => $"${Tax:F2}";
        public string TotalText => $"${SubtotalAfterDiscount + Tax:F2}";
        public string CartCountText => $"{CartItems.Count} ↑";

        // ── Panel de productos: visible / ancho ──

        private bool _isProductsPanelVisible = true;
        public bool IsProductsPanelVisible
        {
            get => _isProductsPanelVisible;
            set
            {
                if (_isProductsPanelVisible != value)
                {
                    _isProductsPanelVisible = value;
                    OnPropertyChanged();
                    OnPropertyChanged(nameof(ProductsPanelVisibilityText));
                }
            }
        }
        public string ProductsPanelVisibilityText => IsProductsPanelVisible ? "◀  Ocultar panel" : "Mostrar panel  ▶";

        // ── Columnas del panel de productos (preferencia del usuario) ──

        private int _preferredSpan = 2;
        public int PreferredSpan
        {
            get => _preferredSpan;
            set
            {
                if (_preferredSpan != value)
                {
                    _preferredSpan = Math.Clamp(value, 2, 4);
                    OnPropertyChanged();
                    OnPropertyChanged(nameof(IsSpan2));
                    OnPropertyChanged(nameof(IsSpan3));
                    OnPropertyChanged(nameof(IsSpan4));
                }
            }
        }
        public bool IsSpan2 => PreferredSpan == 2;
        public bool IsSpan3 => PreferredSpan == 3;
        public bool IsSpan4 => PreferredSpan == 4;

        private int _maxSpan = 4;
        public int MaxSpan
        {
            get => _maxSpan;
            set
            {
                if (_maxSpan != value)
                {
                    _maxSpan = value;
                    OnPropertyChanged();
                    OnPropertyChanged(nameof(IsSpan4Available));
                }
            }
        }
        public bool IsSpan4Available => _maxSpan >= 4;

        // ── Tipo de cambio ──

        private decimal _exchangeRate = 510.00m;
        public decimal ExchangeRate
        {
            get => _exchangeRate;
            set
            {
                if (_exchangeRate != value)
                {
                    _exchangeRate = value;
                    OnPropertyChanged();
                    OnPropertyChanged(nameof(ExchangeRateText));
                    RecalculateTotal();
                }
            }
        }
        public string ExchangeRateText => $"₡{ExchangeRate:F2}";

        // ── Totales en colones ──

        public string SubtotalText => $"${Subtotal:F2}";
        public string SubtotalColonesText => $"₡{Math.Round(Subtotal * _exchangeRate):N0}";
        public string TaxColonesText => $"₡{Math.Round(Tax * _exchangeRate):N0}";
        public string TotalColonesText => $"₡{Math.Round((SubtotalAfterDiscount + Tax) * _exchangeRate):N0}";

        public MainViewModel(IDialogService dialogService)
        {
            _dialogService = dialogService;
            AddProductCommand = new Command<ProductModel>(AddProduct);
            IncrementCommand = new Command<CartItemModel>(Increment);
            DecrementCommand = new Command<CartItemModel>(Decrement);
            ClearCartCommand = new Command(ClearCart);
            InvoiceCommand = new Command(async () => await InvoiceAsync());
            SearchProductCommand = new Command(FilterProducts);
            SelectCategoryCommand = new Command<string>(SelectCategory);
            SelectTabCommand = new Command<string>(SelectTab);
            ApplyDiscountCommand = new Command(async () => await ApplyDiscountAsync());
            ToggleProductsPanelCommand = new Command(() => IsProductsPanelVisible = !IsProductsPanelVisible);
            DecrementProductCommand = new Command<ProductModel>(DecrementProduct);
            SelectSpanCommand = new Command<string>(s => { if (int.TryParse(s, out var n)) PreferredSpan = n; });
            NavigateToClienteCommand = new Command(async () => await Shell.Current.GoToAsync("ClientePage"));
            LoadProducts();
        }

        private void LoadProducts()
        {
            // Con imagen (emoji)
            _allProducts.Add(new ProductModel { Emoji = "🫒", Name = "Aceite Vegetal Puro Premium Sin Colesterol Botella 900ml",  Code = "AV900",  Price = "$4.25",  OldPrice = "$5.50",  PriceValue = 4.25m,  Category = "Super",      Stock = 24  });
            _allProducts.Add(new ProductModel { Emoji = "🍿", Name = "Snack Horneado Sabor Natural Sin Conservantes Sin Gluten 200g", Code = "SN200",  Price = "$1.95",  OldPrice = "$2.75",  PriceValue = 1.95m,  Category = "Super",      Stock = 7   });
            _allProducts.Add(new ProductModel { Emoji = "🥛", Name = "Leche Entera Litro",     Code = "LE001",  Price = "$1.80",  OldPrice = "$2.25",  PriceValue = 1.80m,  Category = "Super",      Stock = 3   });
            _allProducts.Add(new ProductModel { Emoji = "🍪", Name = "Galleta Snack 200g",     Code = "GS200",  Price = "$1.95",                       PriceValue = 1.95m,  Category = "Super",      Stock = 18  });
            _allProducts.Add(new ProductModel { Emoji = "🥤", Name = "Coca Cola 2 Litros",     Code = "CC002",  Price = "$2.40",  OldPrice = "$3.20",  PriceValue = 2.40m,  Category = "Super",      Stock = 0   });
            _allProducts.Add(new ProductModel { Emoji = "🔩", Name = "Caja Tornillos 3/4\"",   Code = "CT034",  Price = "$5.00",                       PriceValue = 5.00m,  Category = "Ferreteria", Stock = 12  });
            _allProducts.Add(new ProductModel { Emoji = "📦", Name = "Caja Clavos 2x1\"",      Code = "CC2X1",  Price = "$4.75",                       PriceValue = 4.75m,  Category = "Ferreteria", Stock = 2   });
            _allProducts.Add(new ProductModel { Emoji = "🔨", Name = "Martillo Profesional Mango Antideslizante Fibra de Vidrio 16oz", Code = "MP16Z",  Price = "$7.50",                       PriceValue = 7.50m,  Category = "Ferreteria", Stock = 5   });
            _allProducts.Add(new ProductModel { Emoji = "🩴", Name = "Sandalias Hombre",       Code = "SH001",  Price = "$12.50", OldPrice = "$16.99", PriceValue = 12.50m, Category = "Calzado",    Stock = 9   });
            _allProducts.Add(new ProductModel { Emoji = "🧣", Name = "Sandalias Mujer",        Code = "SM001",  Price = "$12.50",                      PriceValue = 12.50m, Category = "Calzado",    Stock = 15  });
            _allProducts.Add(new ProductModel { Emoji = "👟", Name = "Zapatillas Deportivas Amortiguación Avanzada Running Unisex", Code = "299721", Price = "$45.00", OldPrice = "$59.99", PriceValue = 45.00m, Category = "Calzado",    Stock = 4   });
            _allProducts.Add(new ProductModel { Emoji = "🛋️", Name = "Cojín Decorativo",       Code = "CD004",  Price = "$8.99",                       PriceValue = 8.99m,  Category = "Hogar",      Stock = 11  });
            _allProducts.Add(new ProductModel { Emoji = "🕯️", Name = "Vela Aromática",         Code = "VA010",  Price = "$3.50",  OldPrice = "$4.99",  PriceValue = 3.50m,  Category = "Hogar",      Stock = 6   });
            _allProducts.Add(new ProductModel { Emoji = "🧹", Name = "Escoba Industrial",      Code = "EI001",  Price = "$5.25",                       PriceValue = 5.25m,  Category = "Hogar",      Stock = 8   });

            // Sin imagen
            _allProducts.Add(new ProductModel { Name = "Arroz Grano Largo Suelto Cocción Rápida Seleccionado Origen Nacional 1kg", Code = "AR001",  Price = "$1.10",                       PriceValue = 1.10m,  Category = "Super",      Stock = 50  });
            _allProducts.Add(new ProductModel { Name = "Azúcar Blanca 2kg",         Code = "AZ002",  Price = "$1.90",                       PriceValue = 1.90m,  Category = "Super",      Stock = 32  });
            _allProducts.Add(new ProductModel { Name = "Sal Refinada 1kg",          Code = "SA001",  Price = "$0.75",                       PriceValue = 0.75m,  Category = "Super",      Stock = 40  });
            _allProducts.Add(new ProductModel { Name = "Café Molido 250g",          Code = "CA025",  Price = "$3.20",  OldPrice = "$4.00",  PriceValue = 3.20m,  Category = "Super",      Stock = 14  });
            _allProducts.Add(new ProductModel { Name = "Frijoles Negros 500g",      Code = "FN050",  Price = "$1.40",                       PriceValue = 1.40m,  Category = "Super",      Stock = 28  });
            _allProducts.Add(new ProductModel { Name = "Cinta Métrica 5m",          Code = "CM005",  Price = "$3.80",                       PriceValue = 3.80m,  Category = "Ferreteria", Stock = 7   });
            _allProducts.Add(new ProductModel { Name = "Llave Ajustable Acero Cromo Vanadio Cabeza Giratoria 30mm 10 Pulgadas", Code = "LA010",  Price = "$6.50",                       PriceValue = 6.50m,  Category = "Ferreteria", Stock = 3   });
            _allProducts.Add(new ProductModel { Name = "Pintura Blanca 1L",         Code = "PB001",  Price = "$5.90",  OldPrice = "$7.50",  PriceValue = 5.90m,  Category = "Ferreteria", Stock = 1   });
            _allProducts.Add(new ProductModel { Name = "Tapón PVC 1/2\"",           Code = "TP012",  Price = "$0.45",                       PriceValue = 0.45m,  Category = "Ferreteria", Stock = 100 });
            _allProducts.Add(new ProductModel { Name = "Calcetines Deportivos",     Code = "CD010",  Price = "$2.50",                       PriceValue = 2.50m,  Category = "Calzado",    Stock = 22  });
            _allProducts.Add(new ProductModel { Name = "Plantillas Ortopédicas",    Code = "PO001",  Price = "$4.75",  OldPrice = "$6.00",  PriceValue = 4.75m,  Category = "Calzado",    Stock = 5   });
            _allProducts.Add(new ProductModel { Name = "Limpiador Multiusos 500ml", Code = "LM050",  Price = "$2.20",                       PriceValue = 2.20m,  Category = "Hogar",      Stock = 19  });
            _allProducts.Add(new ProductModel { Name = "Foco LED 9W",               Code = "FL009",  Price = "$1.80",  OldPrice = "$2.50",  PriceValue = 1.80m,  Category = "Hogar",      Stock = 0   });

            // Calcular precios en colones
            foreach (var p in _allProducts)
                p.PriceColonesValue = Math.Round(p.PriceValue * _exchangeRate);

            FilterProducts();
        }

        private void FilterProducts()
        {
            var query = _allProducts.AsEnumerable();

            // Filtrar por categoría seleccionada
            if (SelectedCategory != "Todos")
            {
                query = query.Where(p => p.Category == SelectedCategory);
            }

            if (!string.IsNullOrWhiteSpace(ProductSearchText))
            {
                var search = ProductSearchText.Trim();
                query = query.Where(p =>
                    p.Name.Contains(search, StringComparison.OrdinalIgnoreCase) ||
                    p.Code.Contains(search, StringComparison.OrdinalIgnoreCase));
            }

            var filtered = query.ToList();
            Products.Clear();
            foreach (var p in filtered)
                Products.Add(p);
        }

        private void SelectTab(string? tab)
        {
            if (tab is null) return;
            SelectedTab = tab;
        }

        private void SelectCategory(string? category)
        {
            if (category is null) return;
            SelectedCategory = category;
        }

        private void AddProduct(ProductModel? product)
        {
            if (product is null) return;

            var existing = CartItems.FirstOrDefault(c => c.Name == product.Name);
            if (existing is not null)
            {
                existing.Quantity++;
                product.CartQuantity = existing.Quantity;
            }
            else
            {
                CartItems.Add(new CartItemModel
                {
                    Emoji = product.Emoji,
                    Name = product.Name,
                    Code = product.Code,
                    UnitPrice = product.PriceValue,
                    UnitPriceColones = product.PriceColonesValue
                });
                product.CartQuantity = 1;
            }
            RecalculateTotal();
        }

        private void Increment(CartItemModel? item)
        {
            if (item is null) return;
            item.Quantity++;
            var product = _allProducts.FirstOrDefault(p => p.Name == item.Name);
            if (product is not null) product.CartQuantity = item.Quantity;
            RecalculateTotal();
        }

        private void Decrement(CartItemModel? item)
        {
            if (item is null) return;
            item.Quantity--;
            if (item.Quantity <= 0)
                CartItems.Remove(item);
            var product = _allProducts.FirstOrDefault(p => p.Name == item.Name);
            if (product is not null) product.CartQuantity = Math.Max(0, item.Quantity);
            RecalculateTotal();
        }

        private void DecrementProduct(ProductModel? product)
        {
            if (product is null) return;
            var existing = CartItems.FirstOrDefault(c => c.Name == product.Name);
            if (existing is null) return;
            existing.Quantity--;
            if (existing.Quantity <= 0)
                CartItems.Remove(existing);
            product.CartQuantity = Math.Max(0, existing.Quantity);
            RecalculateTotal();
        }

        private void ClearCart()
        {
            CartItems.Clear();
            DiscountPercent = 0;
            foreach (var p in _allProducts)
                p.CartQuantity = 0;
            RecalculateTotal();
        }

        private async Task InvoiceAsync()
        {
            if (CartItems.Count == 0)
            {
                await _dialogService.AlertAsync("Aviso", "El carrito está vacío.", "OK");
                return;
            }

            var total = SubtotalAfterDiscount + Tax;
            var confirm = await _dialogService.ConfirmAsync("Facturar",
                $"¿Confirmar factura por ${total:F2}?\n" +
                $"Artículos: {CartItems.Count}\n" +
                $"Descuento: {DiscountPercent}%",
                "Confirmar", "Cancelar");

            if (confirm)
            {
                await _dialogService.AlertAsync("✅ Facturado", $"Factura generada por ${total:F2}", "OK");
                ClearCart();
            }
        }

        private async Task ApplyDiscountAsync()
        {
            var result = await _dialogService.PromptAsync("Descuento", "Ingrese el porcentaje de descuento:",
                accept: "Aplicar",
                cancel: "Cancelar",
                maxLength: 3,
                keyboard: Keyboard.Numeric,
                initialValue: DiscountPercent.ToString());

            if (result is not null && int.TryParse(result, out var percent))
            {
                DiscountPercent = percent;
                RecalculateTotal();
            }
        }

        private void RecalculateTotal()
        {
            Subtotal = CartItems.Sum(c => c.UnitPrice * c.Quantity);
            OnPropertyChanged(nameof(CartCountText));
            OnPropertyChanged(nameof(SubtotalText));
            OnPropertyChanged(nameof(SubtotalColonesText));
            OnPropertyChanged(nameof(TaxColonesText));
            OnPropertyChanged(nameof(TotalColonesText));
        }

        public event PropertyChangedEventHandler? PropertyChanged;
        protected void OnPropertyChanged([CallerMemberName] string? name = null)
            => PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(name));
    }
}
