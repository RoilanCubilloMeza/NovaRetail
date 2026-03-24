using NovaRetail.Models;

namespace NovaRetail.Data
{
    public interface ISalesRepService
    {
        Task<List<SalesRepModel>> GetAllAsync();
    }
}
