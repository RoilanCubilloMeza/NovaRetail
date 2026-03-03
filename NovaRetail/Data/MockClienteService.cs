using NovaRetail.Models;

namespace NovaRetail.Data
{
    public class MockClienteService : IClienteService
    {
        private readonly Dictionary<string, ClienteModel> _store = new();

        public async Task<ClienteModel?> BuscarPorIdAsync(string clienteId)
        {
            await Task.Delay(300);
            return _store.TryGetValue(clienteId, out var cliente) ? cliente : null;
        }

        public async Task<ClienteModel?> SincronizarHaciendaAsync(string clienteId)
        {
            await Task.Delay(1500);
            if (clienteId.Length < 9) return null;
            return new ClienteModel
            {
                ClientId = clienteId,
                Name    = "Nombre Sugerido (Hacienda)"
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
    }
}
