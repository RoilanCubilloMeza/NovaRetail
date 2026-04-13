using System.ComponentModel;
using System.Globalization;
using System.Runtime.CompilerServices;
using Newtonsoft.Json;

namespace NovaRetail.Models;

public class OpenLedgerEntryModel : INotifyPropertyChanged
{
    private bool _isSelected;
    private string _amountToApplyText = "0,00";
    private bool _suppressSync;

    [JsonProperty("ledgerEntryID")]
    public int LedgerEntryID { get; set; }

    [JsonProperty("postingDate")]
    public string PostingDate { get; set; } = string.Empty;

    [JsonProperty("dueDate")]
    public string DueDate { get; set; } = string.Empty;

    [JsonProperty("ledgerTypeName")]
    public string LedgerTypeName { get; set; } = string.Empty;

    [JsonProperty("documentTypeName")]
    public string DocumentTypeName { get; set; } = string.Empty;

    [JsonProperty("description")]
    public string Description { get; set; } = string.Empty;

    [JsonProperty("storeID")]
    public int StoreID { get; set; }

    [JsonProperty("reference")]
    public string Reference { get; set; } = string.Empty;

    [JsonProperty("amount")]
    public decimal Amount { get; set; }

    [JsonProperty("balance")]
    public decimal Balance { get; set; }

    public string AmountText => $"₡{Amount:N2}";
    public string BalanceText => $"₡{Balance:N2}";

    public bool IsSelected
    {
        get => _isSelected;
        set
        {
            if (_isSelected == value) return;
            _isSelected = value;
            OnPropertyChanged();

            if (!_suppressSync)
            {
                _suppressSync = true;
                if (value && AmountToApply <= 0)
                    AmountToApplyText = Balance.ToString("N2", CultureInfo.InvariantCulture);
                else if (!value)
                    AmountToApplyText = "0,00";
                _suppressSync = false;
            }

            ValueChanged?.Invoke();
        }
    }

    public string AmountToApplyText
    {
        get => _amountToApplyText;
        set
        {
            if (_amountToApplyText == value) return;
            _amountToApplyText = value;
            OnPropertyChanged();
            OnPropertyChanged(nameof(AmountToApply));

            if (!_suppressSync)
            {
                _suppressSync = true;
                IsSelected = AmountToApply > 0;
                _suppressSync = false;
            }

            ValueChanged?.Invoke();
        }
    }

    public decimal AmountToApply
    {
        get
        {
            // Remove thousands separators (comma or dot) leaving only the decimal dot
            var text = (_amountToApplyText ?? string.Empty).Trim();
            // If has both comma and dot, determine which is the decimal separator
            bool hasComma = text.Contains(',');
            bool hasDot = text.Contains('.');
            if (hasComma && hasDot)
            {
                // "46,200.00" or "46.200,00" — last separator is decimal
                int lastComma = text.LastIndexOf(',');
                int lastDot = text.LastIndexOf('.');
                if (lastComma > lastDot)
                    text = text.Replace(".", "").Replace(",", "."); // 46.200,00 → 46200.00
                else
                    text = text.Replace(",", ""); // 46,200.00 → 46200.00
            }
            else if (hasComma)
            {
                text = text.Replace(",", "."); // "46200,00" → "46200.00"
            }
            return decimal.TryParse(text, NumberStyles.Any, CultureInfo.InvariantCulture, out var val)
                ? Math.Min(val, Balance) : 0;
        }
    }

    /// <summary>Raised when IsSelected or AmountToApply changes, so parent VM can recalculate totals.</summary>
    public event Action? ValueChanged;

    public event PropertyChangedEventHandler? PropertyChanged;
    protected void OnPropertyChanged([CallerMemberName] string? name = null)
        => PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(name));
}

public class AbonoPaymentRequest
{
    public string AccountNumber { get; set; } = string.Empty;
    public decimal TotalAmount { get; set; }
    public int CashierId { get; set; }
    public int StoreId { get; set; }
    public int TenderId { get; set; }
    public string Comment { get; set; } = string.Empty;
    public string Reference { get; set; } = string.Empty;
    public List<AbonoApplicationItem> Applications { get; set; } = new();
}

public class AbonoApplicationItem
{
    public int LedgerEntryID { get; set; }
    public decimal Amount { get; set; }
    public decimal EntryBalance { get; set; }
}
