namespace NovaRetail.ViewModels
{
    public partial class MainViewModel
    {
        private void OpenAdminActionMenu()
        {
            if (!CanAccessAdminActions || HasBlockingOverlayVisible())
                return;

            AdminActionMenuVm.Load(
                title: "Administración",
                subtitle: "Seleccione el área que desea abrir.",
                primaryText: "Dashboard",
                secondaryText: "Mantenimientos",
                primaryIcon: "📊",
                secondaryIcon: "🔧",
                accentColor: "#0891B2",
                softColor: "#FDF2F8",
                strokeColor: "#FBCFE8",
                labelColor: "#9D174D");

            IsAdminActionMenuVisible = true;
        }

        private async Task OpenManagerDashboardFromAdminMenuAsync()
        {
            IsAdminActionMenuVisible = false;

            if (!CanViewManagerDashboard)
            {
                await _dialogService.AlertAsync("Dashboard", "No tiene permisos para ver el dashboard.", "OK");
                return;
            }

            await Shell.Current.GoToAsync("ManagerDashboardPage");
        }

        private async Task OpenMantenimientosFromAdminMenuAsync()
        {
            IsAdminActionMenuVisible = false;

            if (!CanAccessParametros)
                return;

            await Shell.Current.GoToAsync("MantenimientosPage");
        }
    }
}
