using System.ComponentModel;
using System.Runtime.CompilerServices;
using System.Windows.Input;

namespace NovaRetail.ViewModels;

public sealed class OrderActionMenuViewModel : INotifyPropertyChanged
{
    private string _titleText = string.Empty;
    private string _subtitleText = string.Empty;
    private string _primaryText = string.Empty;
    private string _secondaryText = string.Empty;
    private string _primaryIcon = string.Empty;
    private string _secondaryIcon = string.Empty;
    private Color _accentColor = Color.FromArgb("#2563EB");
    private Color _softColor = Color.FromArgb("#EFF6FF");
    private Color _strokeColor = Color.FromArgb("#BFDBFE");
    private Color _labelColor = Color.FromArgb("#1D4ED8");

    public string TitleText
    {
        get => _titleText;
        private set { if (_titleText != value) { _titleText = value; OnPropertyChanged(); } }
    }

    public string SubtitleText
    {
        get => _subtitleText;
        private set { if (_subtitleText != value) { _subtitleText = value; OnPropertyChanged(); } }
    }

    public string PrimaryText
    {
        get => _primaryText;
        private set { if (_primaryText != value) { _primaryText = value; OnPropertyChanged(); } }
    }

    public string SecondaryText
    {
        get => _secondaryText;
        private set { if (_secondaryText != value) { _secondaryText = value; OnPropertyChanged(); } }
    }

    public string PrimaryIcon
    {
        get => _primaryIcon;
        private set { if (_primaryIcon != value) { _primaryIcon = value; OnPropertyChanged(); } }
    }

    public string SecondaryIcon
    {
        get => _secondaryIcon;
        private set { if (_secondaryIcon != value) { _secondaryIcon = value; OnPropertyChanged(); } }
    }

    public Color AccentColor
    {
        get => _accentColor;
        private set { if (_accentColor != value) { _accentColor = value; OnPropertyChanged(); } }
    }

    public Color SoftColor
    {
        get => _softColor;
        private set { if (_softColor != value) { _softColor = value; OnPropertyChanged(); } }
    }

    public Color StrokeColor
    {
        get => _strokeColor;
        private set { if (_strokeColor != value) { _strokeColor = value; OnPropertyChanged(); } }
    }

    public Color LabelColor
    {
        get => _labelColor;
        private set { if (_labelColor != value) { _labelColor = value; OnPropertyChanged(); } }
    }

    public ICommand PrimaryCommand { get; }
    public ICommand SecondaryCommand { get; }
    public ICommand CloseCommand { get; }

    public event Func<Task>? RequestPrimary;
    public event Func<Task>? RequestSecondary;
    public event Action? RequestClose;

    public OrderActionMenuViewModel()
    {
        PrimaryCommand = new Command(async () =>
        {
            if (RequestPrimary is not null)
                await RequestPrimary.Invoke();
        });
        SecondaryCommand = new Command(async () =>
        {
            if (RequestSecondary is not null)
                await RequestSecondary.Invoke();
        });
        CloseCommand = new Command(() => RequestClose?.Invoke());
    }

    public void Load(
        string title,
        string subtitle,
        string primaryText,
        string secondaryText,
        string primaryIcon,
        string secondaryIcon,
        string accentColor,
        string softColor,
        string strokeColor,
        string labelColor)
    {
        TitleText = title;
        SubtitleText = subtitle;
        PrimaryText = string.IsNullOrWhiteSpace(primaryIcon) ? primaryText : $"{primaryIcon} {primaryText}";
        SecondaryText = secondaryText;
        PrimaryIcon = primaryIcon;
        SecondaryIcon = secondaryIcon;
        AccentColor = Color.FromArgb(accentColor);
        SoftColor = Color.FromArgb(softColor);
        StrokeColor = Color.FromArgb(strokeColor);
        LabelColor = Color.FromArgb(labelColor);
    }

    public event PropertyChangedEventHandler? PropertyChanged;
    private void OnPropertyChanged([CallerMemberName] string? name = null)
        => PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(name));
}
