using NovaRetail.Models;

namespace NovaRetail.Data
{
    public interface IStoreConfigService
    {
        Task<StoreConfigModel> GetConfigAsync();
        Task<List<TenderModel>> GetTendersAsync();
    }
}
