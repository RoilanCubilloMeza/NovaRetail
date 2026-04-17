using NovaRetail.Models;
using System.Collections.ObjectModel;

namespace NovaRetail.Services;

public interface IPricingService
{
    OrderTotals CalculateOrderTotals(
        ObservableCollection<CartItemModel> cartItems,
        int discountPercent,
        decimal exchangeRate,
        bool isTaxIncluded);

    LineTotals CalculateLineTotals(CartItemModel item, int discountPercent, bool isTaxIncluded);

    decimal ConvertFromColones(decimal amount, decimal exchangeRate);
}

public sealed class PricingService : IPricingService
{
    public OrderTotals CalculateOrderTotals(
        ObservableCollection<CartItemModel> cartItems,
        int discountPercent,
        decimal exchangeRate,
        bool isTaxIncluded)
    {
        decimal subtotalBaseColones = 0m;
        decimal taxAmountColones = 0m;
        decimal totalAmountColones = 0m;
        decimal discountAmountColones = 0m;
        decimal exonerationAmountColones = 0m;

        foreach (var item in cartItems)
        {
            var lineTotals = CalculateLineTotals(item, discountPercent, isTaxIncluded);
            subtotalBaseColones += lineTotals.SubtotalBaseColones;
            taxAmountColones += lineTotals.TaxColones;
            totalAmountColones += lineTotals.TotalColones;
            discountAmountColones += lineTotals.DiscountColones;
            exonerationAmountColones += lineTotals.ExonerationColones;
        }

        var subtotalCol = Math.Round(subtotalBaseColones, 2);
        var taxCol = Math.Round(taxAmountColones, 2);
        var totalCol = Math.Round(totalAmountColones, 2);
        var discountCol = Math.Round(discountAmountColones, 2);
        var exonerationCol = Math.Round(exonerationAmountColones, 2);

        return new OrderTotals
        {
            SubtotalColones = subtotalCol,
            TaxColones = taxCol,
            TotalColones = totalCol,
            DiscountColones = discountCol,
            ExonerationColones = exonerationCol,
            Subtotal = ConvertFromColones(subtotalCol, exchangeRate),
            Tax = ConvertFromColones(taxCol, exchangeRate),
            Total = ConvertFromColones(totalCol, exchangeRate),
            DiscountAmount = ConvertFromColones(discountCol, exchangeRate),
            ExonerationAmount = ConvertFromColones(exonerationCol, exchangeRate)
        };
    }

    public LineTotals CalculateLineTotals(CartItemModel item, int discountPercent, bool isTaxIncluded)
    {
        var originalGrossColones = item.EffectivePriceColones * item.Quantity;
        var itemDiscountFactor = 1m - (item.DiscountPercent / 100m);
        var ticketDiscountFactor = 1m - (discountPercent / 100m);
        var displayedGrossAfterItemDiscount = originalGrossColones * itemDiscountFactor;
        var displayedGrossAfterAllDiscounts = displayedGrossAfterItemDiscount * ticketDiscountFactor;
        var originalTaxRate = item.TaxPercentage / 100m;
        var effectiveTaxRate = item.EffectiveTaxPercentage / 100m;

        if (isTaxIncluded)
        {
            var divisor = 1m + originalTaxRate;
            if (divisor <= 0m)
                divisor = 1m;

            var subtotalBaseColones = displayedGrossAfterItemDiscount / divisor;
            var discountedBaseColones = displayedGrossAfterAllDiscounts / divisor;
            var taxColones = discountedBaseColones * effectiveTaxRate;
            var totalColones = discountedBaseColones + taxColones;

            return new LineTotals
            {
                SubtotalBaseColones = subtotalBaseColones,
                TaxColones = taxColones,
                TotalColones = totalColones,
                DiscountColones = originalGrossColones - displayedGrossAfterAllDiscounts,
                ExonerationColones = Math.Max(0m, discountedBaseColones * (originalTaxRate - effectiveTaxRate))
            };
        }

        var subtotalBase = displayedGrossAfterItemDiscount;
        var discountedBase = displayedGrossAfterAllDiscounts;
        var taxAmount = discountedBase * effectiveTaxRate;

        return new LineTotals
        {
            SubtotalBaseColones = subtotalBase,
            TaxColones = taxAmount,
            TotalColones = discountedBase + taxAmount,
            DiscountColones = originalGrossColones - displayedGrossAfterAllDiscounts,
            ExonerationColones = Math.Max(0m, discountedBase * (originalTaxRate - effectiveTaxRate))
        };
    }

    public decimal ConvertFromColones(decimal amount, decimal exchangeRate)
        => exchangeRate > 0 ? Math.Round(amount / exchangeRate, 4) : amount;
}

public sealed class OrderTotals
{
    public decimal SubtotalColones { get; init; }
    public decimal TaxColones { get; init; }
    public decimal TotalColones { get; init; }
    public decimal DiscountColones { get; init; }
    public decimal ExonerationColones { get; init; }
    public decimal Subtotal { get; init; }
    public decimal Tax { get; init; }
    public decimal Total { get; init; }
    public decimal DiscountAmount { get; init; }
    public decimal ExonerationAmount { get; init; }
}

public sealed class LineTotals
{
    public decimal SubtotalBaseColones { get; set; }
    public decimal TaxColones { get; set; }
    public decimal TotalColones { get; set; }
    public decimal DiscountColones { get; set; }
    public decimal ExonerationColones { get; set; }
}
