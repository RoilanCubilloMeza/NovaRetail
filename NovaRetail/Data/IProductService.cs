using NovaRetail.Models;

namespace NovaRetail.Data;

/// <summary>Contrato para consulta de productos y códigos de motivo.</summary>
public interface IProductService
{
    Task<List<ProductModel>> GetProductsAsync(int page, int pageSize, decimal exchangeRate, int storeId = 1);
    Task<List<ProductModel>> SearchAsync(string criteria, int top, decimal exchangeRate);
    Task<List<ReasonCodeModel>> GetReasonCodesAsync(int type);
    Task<List<ReasonCodeModel>> GetExonerationDocumentTypesAsync();
    Task<int> GetProductCountAsync(int storeId = 1);
}
