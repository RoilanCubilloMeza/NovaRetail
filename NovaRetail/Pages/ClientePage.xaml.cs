using NovaRetail.ViewModels;

namespace NovaRetail.Pages
{
    public partial class ClientePage : ContentPage
    {
        public ClientePage(ClienteViewModel vm)
        {
            InitializeComponent();
            BindingContext = vm;
        }
    }
}
