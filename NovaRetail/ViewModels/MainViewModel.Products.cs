using NovaRetail.Data;
using NovaRetail.Models;
using NovaRetail.Services;
using NovaRetail.State;
using System.Globalization;

namespace NovaRetail.ViewModels
{
    public partial class MainViewModel
    {
        private async Task LoadCategoryProductsAsync(string category)
        {
            if (string.IsNullOrWhiteSpace(category) || category == CategoryKeys.Todos)
                return;

            IsSearchingProducts = true;
            try
            {
                List<ProductModel> products;

                var deptId = CategoryKeys.GetDepartmentID(category);
                if (deptId > 0)
                    products = await _productService.SearchByDepartmentAsync(deptId, 300, _exchangeRate);
                else
                    products = await _productService.SearchAsync(category, 300, _exchangeRate);

                if (products.Count == 0)
                    return;

                StampNonInventoryFlag(products);
                _allProducts.Clear();
                _allProducts.AddRange(products);
                _loadedItemsPage = 0;
                _canLoadMoreFromApi = false;
                FilterProducts();
            }
            catch
            {
            }
            finally
            {
                IsSearchingProducts = false;
            }
        }

        private async Task RefreshVisibleCatalogAsync()
        {
            if (!string.IsNullOrWhiteSpace(ProductSearchText))
            {
                FilterProducts();

                if (NormalizeText(ProductSearchText).Length >= 3)
                {
                    _searchCts.Cancel();
                    _searchCts = new CancellationTokenSource();
                    var cts = _searchCts;
                    await SearchFromApiAsync(ProductSearchText, cts.Token);
                }

                return;
            }

            if (string.Equals(SelectedCategory, CategoryKeys.Todos, StringComparison.OrdinalIgnoreCase))
            {
                await LoadProductsAsync();
                return;
            }

            await LoadCategoryProductsAsync(SelectedCategory);
            FilterProducts();
        }

        private async Task ResetCatalogAfterCheckoutAsync()
        {
            _searchCts.Cancel();
            _searchCts = new CancellationTokenSource();

            _appStore.Dispatch(new SetProductSearchTextAction(string.Empty));
            _appStore.Dispatch(new SetSelectedTabAction(TabKeys.Categorias));
            _appStore.Dispatch(new SetSelectedCategoryAction(CategoryKeys.Todos));

            FilterProducts();
            await LoadProductsAsync();
        }

        private async Task SearchOrAddProductByCodeAsync()
        {
            if (_isSearchingByCode) return;
            _isSearchingByCode = true;
            IsSearchingProducts = true;

            // Cancel any pending debounced search to avoid duplicate API calls
            _searchCts.Cancel();
            _searchCts = new CancellationTokenSource();

            try
            {
                var parsedInput = ParseCodeAndQuantity(ProductSearchText);
                var code = parsedInput.Code;
                var quantityToAdd = parsedInput.Quantity;

                if (string.IsNullOrWhiteSpace(code))
                {
                    FilterProducts();
                    return;
                }

                // Intentar primero búsqueda exacta (código de barras / código de producto)
                var product = FindProductByCode(_allProducts, code);

                if (product is null)
                {
                    try
                    {
                        var results = await _productService.SearchAsync(code, 20, _exchangeRate);
                        product = FindProductByCode(results, code);

                        // Si no hay match exacto y el código parece un código de barras (solo dígitos, 8-14 caracteres), intentar sin check-digit
                        if (product is null && IsBarcodeFormat(code))
                        {
                            var codeWithoutCheckDigit = code[..^1];
                            product = FindProductByCode(results, codeWithoutCheckDigit);
                        }

                        if (product is not null && !ContainsProductCode(_allProducts, product.Code))
                        {
                            StampNonInventoryFlag(new[] { product });
                            _allProducts.Add(product);
                        }

                        // If no exact match found, merge search results for display
                        if (product is null && results.Count > 0)
                        {
                            StampNonInventoryFlag(results);
                            _allProducts.Clear();
                            _allProducts.AddRange(results);
                            _loadedItemsPage = 0;
                            _canLoadMoreFromApi = false;
                            FilterProducts();
                            return;
                        }
                    }
                    catch
                    {
                    }
                }

                if (product is not null)
                {
                    if (IsNonInventoryItem(product.ItemType))
                        OpenServicePriceEntry(product);
                    else
                        AddProduct(product, quantityToAdd);
                    ProductSearchText = string.Empty;
                    return;
                }

                FilterProducts();
            }
            finally
            {
                _isSearchingByCode = false;
                IsSearchingProducts = false;
            }
        }

        private static bool IsBarcodeFormat(string code)
            => code.Length >= 8 && code.Length <= 14 && code.All(char.IsDigit);

        private static ProductModel? FindProductByCode(IEnumerable<ProductModel> products, string code)
            => products.FirstOrDefault(p => string.Equals((p.Code ?? string.Empty).Trim(), code, StringComparison.OrdinalIgnoreCase));

        private static bool ContainsProductCode(IEnumerable<ProductModel> products, string? code)
        {
            if (string.IsNullOrWhiteSpace(code))
                return false;

            return products.Any(p => string.Equals((p.Code ?? string.Empty).Trim(), code.Trim(), StringComparison.OrdinalIgnoreCase));
        }

        private static (string Code, decimal Quantity) ParseCodeAndQuantity(string? input)
        {
            var text = input?.Trim() ?? string.Empty;
            if (string.IsNullOrWhiteSpace(text))
                return (string.Empty, 1m);

            decimal quantity = 1m;
            var code = text;

            var separatorIndex = text.IndexOf('*');
            if (separatorIndex < 0)
                separatorIndex = text.IndexOf('x');
            if (separatorIndex < 0)
                separatorIndex = text.IndexOf('X');

            if (separatorIndex > 0 && separatorIndex < text.Length - 1)
            {
                var codePart = text[..separatorIndex].Trim();
                var quantityPart = text[(separatorIndex + 1)..].Trim();

                if (TryParseQuantity(quantityPart, out var parsedQuantity))
                {
                    code = codePart;
                    quantity = parsedQuantity;
                }
            }

            return (code, quantity);
        }

        private static bool TryParseQuantity(string value, out decimal quantity)
        {
            quantity = 1m;

            if (decimal.TryParse(value, NumberStyles.Number, CultureInfo.CurrentCulture, out var parsed) ||
                decimal.TryParse(value, NumberStyles.Number, CultureInfo.InvariantCulture, out parsed))
            {
                if (parsed > 0)
                {
                    quantity = Math.Floor(parsed);
                    if (quantity < 1m)
                        quantity = 1m;
                    return true;
                }
            }

            return false;
        }

        private static decimal GetMaxAvailableQuantity(decimal stock)
        {
            if (stock <= 0m)
                return 0m;

            return Math.Floor(stock);
        }

        private bool IsNonInventoryItem(int itemType)
            => _nonInventoryItemTypes.Count > 0 && _nonInventoryItemTypes.Contains(itemType);

        private void StampNonInventoryFlag(IEnumerable<ProductModel> products)
        {
            if (_nonInventoryItemTypes.Count == 0) return;
            foreach (var p in products)
                p.IsNonInventory = _nonInventoryItemTypes.Contains(p.ItemType);
        }

        private static HashSet<int> ParseNonInventoryItemTypes(string? value)
        {
            var set = new HashSet<int>();
            if (string.IsNullOrWhiteSpace(value)) return set;
            foreach (var part in value.Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries))
            {
                if (int.TryParse(part, out var id))
                    set.Add(id);
            }
            return set;
        }

        private void ShowStockLimitAlert(string? productName, decimal stock)
        {
            var safeProductName = string.IsNullOrWhiteSpace(productName) ? "este producto" : productName.Trim();
            var message = stock <= 0m
                ? $"El producto \"{safeProductName}\" está agotado."
                : $"Solo hay {stock:0.##} unidad(es) disponibles de \"{safeProductName}\".";

            _ = _dialogService.AlertAsync("Stock insuficiente", message, "OK");
        }

        private async Task<bool> LoadProductsAsync(bool loadMore = false)
        {
            if (_isLoadingItems)
                return false;

            if (loadMore && !_canLoadMoreFromApi)
                return false;

            _isLoadingItems = true;
            if (!loadMore) IsSearchingProducts = true;
            var nextPage = loadMore ? _loadedItemsPage + 1 : 1;

            try
            {
                var products = await _productService.GetProductsAsync(nextPage, ProductsPageSize, _exchangeRate, _storeIdFromConfig > 0 ? _storeIdFromConfig : 1);

                if (products.Count == 0)
                {
                    _isLoadingItems = false;
                    if (!loadMore)
                    {
                        IsSearchingProducts = false;
                        _allProducts.Clear();
                        _loadedItemsPage = 0;
                        _canLoadMoreFromApi = false;
                        FilterProducts();
                    }
                    return false;
                }

                if (!loadMore)
                    _allProducts.Clear();

                StampNonInventoryFlag(products);
                _allProducts.AddRange(products);
                _loadedItemsPage = nextPage;
                _canLoadMoreFromApi = products.Count >= ProductsPageSize;

                if (loadMore && CanAppendPagedProductsDirectly())
                    AppendPagedProducts(products);
                else
                    FilterProducts();

                RefreshProductCountText();
                _isLoadingItems = false;
                if (!loadMore) IsSearchingProducts = false;
                return true;
            }
            catch
            {
                _isLoadingItems = false;
                IsSearchingProducts = false;
                return false;
            }
        }

        private void FilterProducts(bool skipSearchFilter = false)
        {
            var query = _allProducts.AsEnumerable();

            // Filtrar por categoría seleccionada
            if (SelectedCategory != CategoryKeys.Todos)
            {
                query = query.Where(p => MatchesCategory(p.Category, SelectedCategory));
            }

            if (!skipSearchFilter && !string.IsNullOrWhiteSpace(ProductSearchText))
            {
                var words = NormalizeText(ProductSearchText)
                    .Split(' ', StringSplitOptions.RemoveEmptyEntries);

                if (words.Length > 0)
                {
                    // Filtrar stop words, pero conservar todas si al hacerlo quedaría vacío
                    var searchWords = SearchSynonyms.RemoveStopWords(words);
                    if (searchWords.Length == 0) searchWords = words;

                    var wordGroups = SearchSynonyms.ExpandSearchWords(searchWords);
                    var totalConcepts = wordGroups.Count;
                    var minMatch = totalConcepts <= 2 ? totalConcepts : totalConcepts - 1;
                    query = query.Where(p =>
                    {
                        var name = NormalizeText(p.Name);
                        var code = NormalizeText(p.Code ?? string.Empty);
                        return wordGroups.Count(g => g.Any(variant =>
                            SearchSynonyms.ContainsWord(name, variant) ||
                            SearchSynonyms.ContainsWord(code, variant))) >= minMatch;
                    });
                }
            }

            var filtered = query
                .OrderByDescending(p => p.Stock > 0)
                .ThenBy(p => p.Name)
                .ToList();

            // Actualizar solo si hay diferencias reales (evitar N eventos innecesarios)
            if (Products.Count == filtered.Count && Products.SequenceEqual(filtered))
                return;

            Products.Clear();
            foreach (var p in filtered)
                Products.Add(p);
        }

        private bool CanAppendPagedProductsDirectly()
        {
            if (!string.IsNullOrWhiteSpace(ProductSearchText))
                return false;

            return string.Equals(SelectedCategory, CategoryKeys.Todos, StringComparison.OrdinalIgnoreCase);
        }

        private void AppendPagedProducts(IEnumerable<ProductModel> pageProducts)
        {
            var page = pageProducts;

            if (!string.Equals(SelectedCategory, CategoryKeys.Todos, StringComparison.OrdinalIgnoreCase))
                page = page.Where(p => MatchesCategory(p.Category, SelectedCategory));

            var existingCodes = Products
                .Select(p => p.Code ?? string.Empty)
                .ToHashSet(StringComparer.OrdinalIgnoreCase);

            foreach (var product in page
                .OrderByDescending(p => p.Stock > 0)
                .ThenBy(p => p.Name))
            {
                var code = product.Code ?? string.Empty;
                if (existingCodes.Add(code))
                    Products.Add(product);
            }
        }

        private static bool MatchesCategory(string productCategory, string selectedCategory)
        {
            return string.Equals(productCategory, selectedCategory, StringComparison.OrdinalIgnoreCase);
        }

        private async Task SearchFromApiAsync(string term, CancellationToken cancellationToken = default)
        {
            var normalized = NormalizeText(term);
            if (string.IsNullOrWhiteSpace(normalized) || normalized.Length < 3)
                return;

            await MainThread.InvokeOnMainThreadAsync(() => IsSearchingProducts = true);
            try
            {
                var products = await _productService.SearchAsync(normalized, 300, _exchangeRate);
                cancellationToken.ThrowIfCancellationRequested();

                if (products.Count == 0)
                    return;

                StampNonInventoryFlag(products);
                _allProducts.Clear();
                _allProducts.AddRange(products);
                _loadedItemsPage = 0;
                _canLoadMoreFromApi = false;

                await MainThread.InvokeOnMainThreadAsync(() => FilterProducts(skipSearchFilter: true));
            }
            catch (OperationCanceledException) { }
            catch
            {
            }
            finally
            {
                await MainThread.InvokeOnMainThreadAsync(() => IsSearchingProducts = false);
            }
        }

        private async Task LoadMoreProductsAsync()
        {
            if (IsLoadingMoreProducts || !_canLoadMoreFromApi)
                return;

            if (!string.IsNullOrWhiteSpace(ProductSearchText))
                return;

            if (!string.Equals(SelectedCategory, CategoryKeys.Todos, StringComparison.OrdinalIgnoreCase))
                return;

            IsLoadingMoreProducts = true;
            try
            {
                await LoadProductsAsync(loadMore: true);
            }
            finally
            {
                IsLoadingMoreProducts = false;
                RefreshProductCountText();
            }
        }

        private async Task LoadProductCountAsync()
        {
            try
            {
                var count = await _productService.GetProductCountAsync(_storeIdFromConfig > 0 ? _storeIdFromConfig : 1);
                TotalApiProducts = count;
            }
            catch
            {
            }
        }

        private void RefreshProductCountText()
        {
            OnPropertyChanged(nameof(LoadedProductCount));
            OnPropertyChanged(nameof(ProductCountText));
            OnPropertyChanged(nameof(CanLoadMore));
            OnPropertyChanged(nameof(LoadProgress));
        }
    }
}
