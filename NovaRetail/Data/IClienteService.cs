using NovaRetail.Models;

namespace NovaRetail.Data;

/// <summary>Contrato para persistencia y consulta de clientes.</summary>
public interface IClienteService
{
    Task<ClienteModel?> BuscarPorIdAsync(string clienteId);
    Task<ClienteModel?> SincronizarHaciendaAsync(string clienteId);
    Task<bool> GuardarAsync(ClienteModel cliente);
    Task<string> BuscarActividadAsync(string codActividad);
    Task<IReadOnlyList<string>> ObtenerTiposClienteAsync();
    Task<IReadOnlyList<string>> ObtenerTiposIdentificacionAsync();
    Task<IReadOnlyList<CustomerLookupModel>> BuscarClientesAsync(string? criteria);
}

