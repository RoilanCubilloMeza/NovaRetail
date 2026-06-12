namespace NovaRetail.Services;

public sealed record CompanyInfo(string Name, string LegalId, string Address, string Phone);

public sealed record CustomerInfo(string Name, string Id);

public sealed record ReceiptPayment(string Method, decimal Amount);

public sealed record ReceiptItem(
    string Code,
    string Name,
    decimal Quantity,
    decimal UnitPrice,
    decimal DiscountRate,
    decimal? ExplicitDiscount,
    decimal TaxRate,
    decimal? ExplicitTax)
{
    public decimal GrossTotal => Math.Round(Quantity * UnitPrice, 2, MidpointRounding.AwayFromZero);
    public decimal Discount => ExplicitDiscount ?? Math.Round(GrossTotal * DiscountRate / 100m, 2, MidpointRounding.AwayFromZero);
    public decimal NetTotal => GrossTotal - Discount;
    public decimal Tax => ExplicitTax ?? Math.Round(NetTotal * TaxRate / 100m, 2, MidpointRounding.AwayFromZero);
    public decimal LineTotal => NetTotal + Tax;
}

public sealed record Receipt(
    CompanyInfo Company,
    CustomerInfo Customer,
    string DocumentType,
    string InvoiceNumber,
    string ElectronicKey,
    DateTime Date,
    string Register,
    string Cashier,
    string Currency,
    IReadOnlyList<ReceiptItem> Items,
    IReadOnlyList<ReceiptPayment> Payments,
    IReadOnlyList<string> FooterLines,
    string FiscalNotice,
    decimal? ExplicitSubtotal = null,
    decimal? ExplicitDiscount = null,
    decimal? ExplicitTax = null,
    decimal? ExplicitTotal = null)
{
    public decimal Subtotal => ExplicitSubtotal ?? Items.Sum(item => item.GrossTotal);
    public decimal Discount => ExplicitDiscount ?? Items.Sum(item => item.Discount);
    public decimal Tax => ExplicitTax ?? Items.Sum(item => item.Tax);
    public decimal Total => ExplicitTotal ?? Items.Sum(item => item.LineTotal);
    public decimal Paid => Payments.Sum(payment => payment.Amount);
    public decimal Change => Math.Max(0m, Paid - Total);
}
