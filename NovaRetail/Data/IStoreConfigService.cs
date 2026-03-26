using NovaRetail.Models;

namespace NovaRetail.Data;

/// <summary>Contrato para obtener la configuración de tienda y formas de pago.</summary>
public interface IStoreConfigService
{
    Task<StoreConfigModel> GetConfigAsync();
    Task<List<TenderModel>> GetTendersAsync();
}
