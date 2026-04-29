namespace NovaRetail.Models;

public class TenderSettingsModel
{
    public int ID { get; set; }
    public string SalesTenderCods { get; set; } = string.Empty;
    public string PaymentsTenderCods { get; set; } = string.Empty;
    public string NCTenderCods { get; set; } = string.Empty;
    public string NCPaymentCods { get; set; } = string.Empty;
    public string NCPaymentChargeCode { get; set; } = string.Empty;
}
