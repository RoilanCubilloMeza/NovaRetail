using NovaRetail.Models;

namespace NovaRetail.Data;

public interface IParametrosService
{
    Task<List<ParametroModel>> GetParametrosAsync();
    Task<bool> SaveParametroAsync(ParametroModel parametro);
    Task<TenderSettingsModel?> GetTenderSettingsAsync();
    Task<bool> SaveTenderSettingsAsync(TenderSettingsModel settings);
}
