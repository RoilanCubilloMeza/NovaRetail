using NovaRetail.Models;
using NovaRetail.Services;

namespace NovaRetail.Data
{
    public class MockClienteService : IClienteService
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
            if (clienteId.Length < 9) return null;

            var datos = await _utilities.GetDatosCedulaAsync(clienteId);
            var nombre = datos?.FullName;
            if (string.IsNullOrWhiteSpace(nombre))
                nombre = string.Join(" ", new[] { datos?.FirstName, datos?.LastName }.Where(x => !string.IsNullOrWhiteSpace(x)));

            return new ClienteModel
            {
                ClientId = clienteId,
                Name = string.IsNullOrWhiteSpace(nombre)
                    ? "Nombre Sugerido (Hacienda)"
                    : nombre
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
            return new[]
            {
                "Contado",
                "Crédito",
                "Gobierno",
                "Exportación"
            };
        }
    }
}
