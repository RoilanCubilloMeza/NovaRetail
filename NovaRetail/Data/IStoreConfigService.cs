using NovaRetail.Models;

namespace NovaRetail.Data;

/// <summary>Contrato para obtener la configuración de tienda, formas de pago y categorías.</summary>
public interface IStoreConfigService
{
    Task<StoreConfigModel> GetConfigAsync();
    Task<List<TenderModel>> GetTendersAsync();
    Task<List<CategoryModel>> GetCategoriesAsync();
    Task<List<CategoryModel>> GetAllCategoriesAsync();
    Task<string> GetCategoryConfigAsync();
    Task<bool> SaveCategoryConfigAsync(string selectedIds);
}
