using NovaRetail.Models;

namespace NovaRetail.Data
{
    public interface IProductService
    {
        Task<List<ProductModel>> GetProductsAsync(int page, int pageSize, decimal exchangeRate);
        Task<List<ProductModel>> SearchAsync(string criteria, int top, decimal exchangeRate);
        Task<List<ReasonCodeModel>> GetReasonCodesAsync(int type);
    }
}
