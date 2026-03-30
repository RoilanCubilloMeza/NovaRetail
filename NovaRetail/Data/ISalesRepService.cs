using NovaRetail.Models;

namespace NovaRetail.Data;

/// <summary>Contrato para consulta de representantes de venta.</summary>
public interface ISalesRepService
{
    Task<List<SalesRepModel>> GetAllAsync();
}
