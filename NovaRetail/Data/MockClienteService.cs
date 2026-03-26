using NovaRetail.Models;
using NovaRetail.Models.Dtos;
using NovaRetail.Services;

namespace NovaRetail.Data;

public sealed class MockClienteService : IClienteService
{
    private readonly Dictionary<string, ClienteModel> _store = new();
    private readonly Utilities _utilities;

    public MockClienteService(Utilities utilities)
    {
        _utilities = utilities;
    }

    public async Task<ClienteModel?> BuscarPorIdAsync(string clienteId)
    {
        await Task.Delay(300);
        return _store.TryGetValue(clienteId, out var cliente) ? cliente : null;
    }

    public async Task<ClienteModel?> SincronizarHaciendaAsync(string clienteId)
    {
        await Task.Delay(1500);

        var datos = await _utilities.GetDatosCedulaAsync(clienteId);
        if (datos is null)
            return null;

        var nombre = datos?.FullName;
        if (string.IsNullOrWhiteSpace(nombre))
            nombre = string.Join(" ", new[] { datos?.FirstName, datos?.LastName }.Where(x => !string.IsNullOrWhiteSpace(x)));

        var actividades = (datos?.Actividades ?? [])
            .Select(a => new
            {
                Codigo = ActivityCodeHelper.Normalize(a.Codigo ?? a.CIIU4),
                Descripcion = string.IsNullOrWhiteSpace(a.Descripcion) ? a.CIIU4desc : a.Descripcion
            })
            .Where(a => !string.IsNullOrWhiteSpace(a.Codigo))
            .Take(5)
            .ToList();

        return new ClienteModel
        {
            ClientId = clienteId,
            Name = string.IsNullOrWhiteSpace(nombre)
                ? "Nombre Sugerido (Hacienda)"
                : nombre,
            ActivityCodes = actividades.Select(a => a.Codigo!).ToList(),
            ActivityCode = string.Join(", ", actividades.Select(a => a.Codigo)),
            ActivityDescription = string.Join("; ", actividades.Select(a => a.Descripcion).Where(x => !string.IsNullOrWhiteSpace(x)))
        };
    }

    public async Task<bool> GuardarAsync(ClienteModel cliente)
    {
        await Task.CompletedTask;
        _store[cliente.ClientId] = cliente;
        return true;
    }

    public async Task<string> BuscarActividadAsync(string codActividad)
    {
        await Task.Delay(300);
        return "Venta al por menor de alimentos, bebidas y tabaco";
    }

    public async Task<IReadOnlyList<string>> ObtenerTiposClienteAsync()
    {
        await Task.Delay(200);
        return
        [
            "Contado",
            "Crédito",
            "Gobierno",
            "Exportación"
        ];
    }
}
