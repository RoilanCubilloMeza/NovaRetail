using System;
using System.ComponentModel;
using System.Runtime.CompilerServices;
using System.Windows.Input;

namespace NovaRetail.ViewModels;

public class DiscountEntryViewModel : INotifyPropertyChanged
{
    private string _percentText = "0";
    private string _inputBuffer = "0";

    public string PercentText
    {
        get => _percentText;
        private set
        {
            if (_percentText != value)
            {
                _percentText = value;
                OnPropertyChanged();
                OnPropertyChanged(nameof(CanConfirm));
                ((Command)OkCommand).ChangeCanExecute();
            }
        }
    }

    public int? SelectedPercent => int.TryParse(PercentText, out var percent)
        ? Math.Clamp(percent, 0, 100)
        : null;

    public bool CanConfirm => SelectedPercent.HasValue;

    public event Action? RequestOk;
    public event Action? RequestCancel;
    public event PropertyChangedEventHandler? PropertyChanged;

    public ICommand KeypadCommand { get; }
    public ICommand OkCommand { get; }
    public ICommand CancelCommand { get; }

    public DiscountEntryViewModel()
    {
        KeypadCommand = new Command<string>(HandleKeypad);
        OkCommand = new Command(() => RequestOk?.Invoke(), () => CanConfirm);
        CancelCommand = new Command(() => RequestCancel?.Invoke());
    }

    public void LoadPercent(decimal currentPercent)
    {
        var percent = (int)Math.Clamp(Math.Round(currentPercent, MidpointRounding.AwayFromZero), 0, 100);
        _inputBuffer = percent.ToString();
        PercentText = _inputBuffer;
    }

    private void HandleKeypad(string? key)
    {
        switch (key)
        {
            case "C":
                _inputBuffer = string.Empty;
                break;
            case "Regresar":
                if (_inputBuffer.Length > 0)
                    _inputBuffer = _inputBuffer[..^1];
                break;
            case "ENT":
                CommitBuffer();
                if (CanConfirm)
                    RequestOk?.Invoke();
                return;
            case ".":
                return;
            default:
                if (!string.IsNullOrWhiteSpace(key) && key.Length == 1 && char.IsDigit(key[0]))
                {
                    _inputBuffer = _inputBuffer == "0"
                        ? key
                        : string.Concat(_inputBuffer, key);
                }
                break;
        }

        CommitBuffer();
    }

    private void CommitBuffer()
    {
        if (string.IsNullOrWhiteSpace(_inputBuffer))
        {
            PercentText = string.Empty;
            return;
        }

        if (!int.TryParse(_inputBuffer, out var percent))
            return;

        percent = Math.Clamp(percent, 0, 100);
        _inputBuffer = percent.ToString();
        PercentText = _inputBuffer;
    }

    protected void OnPropertyChanged([CallerMemberName] string? name = null)
        => PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(name));
}
