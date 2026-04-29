using System.ComponentModel;
using System.Runtime.CompilerServices;
using System.Windows.Input;

namespace NovaRetail.ViewModels;

public sealed class WorkOrderActionViewModel : INotifyPropertyChanged
{
    private string _title = "Seleccionar Acción de Orden";
    private string _subtitle = string.Empty;
    private bool _canPickPartial = true;

    public string Title
    {
        get => _title;
        private set { if (_title != value) { _title = value; OnPropertyChanged(); } }
    }

    public string Subtitle
    {
        get => _subtitle;
        private set { if (_subtitle != value) { _subtitle = value; OnPropertyChanged(); } }
    }

    public bool CanPickPartial
    {
        get => _canPickPartial;
        private set { if (_canPickPartial != value) { _canPickPartial = value; OnPropertyChanged(); } }
    }

    public event Func<Task>? RequestSaveChanges;
    public event Func<Task>? RequestPickComplete;
    public event Func<Task>? RequestPickPartial;
    public event Action? RequestCancel;

    public ICommand SaveChangesCommand { get; }
    public ICommand PickCompleteCommand { get; }
    public ICommand PickPartialCommand { get; }
    public ICommand CancelCommand { get; }

    public WorkOrderActionViewModel()
    {
        SaveChangesCommand = new Command(async () =>
        {
            if (RequestSaveChanges is not null)
                await RequestSaveChanges.Invoke();
        });

        PickCompleteCommand = new Command(async () =>
        {
            if (RequestPickComplete is not null)
                await RequestPickComplete.Invoke();
        });

        PickPartialCommand = new Command(async () =>
        {
            if (RequestPickPartial is not null)
                await RequestPickPartial.Invoke();
        }, () => CanPickPartial);

        CancelCommand = new Command(() => RequestCancel?.Invoke());
    }

    public void Load(int orderId, int itemCount, bool canPickPartial = true)
    {
        Title = "Seleccionar Acción de Orden";
        Subtitle = $"Orden de trabajo #{orderId} con {itemCount} línea(s).";
        CanPickPartial = canPickPartial;
        ((Command)PickPartialCommand).ChangeCanExecute();
    }

    public event PropertyChangedEventHandler? PropertyChanged;

    private void OnPropertyChanged([CallerMemberName] string? propertyName = null)
        => PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
}