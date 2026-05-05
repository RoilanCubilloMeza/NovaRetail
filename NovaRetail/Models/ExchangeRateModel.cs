namespace NovaRetail.Models;

public sealed class ExchangeRateModel
{
    public decimal SaleRate { get; set; }
    public decimal PurchaseRate { get; set; }
    public DateTime RateDate { get; set; }
}
