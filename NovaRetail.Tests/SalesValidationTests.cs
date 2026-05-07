using System.ComponentModel.DataAnnotations;
using NovaAPI.Models;

namespace NovaRetail.Tests;

public sealed class SalesValidationTests
{
    [Fact]
    public void CreateSaleRequest_fails_when_items_or_tenders_are_missing()
    {
        var request = new NovaRetailCreateSaleRequest
        {
            StoreID = 1,
            RegisterID = 1,
            CashierID = 1,
            Items = [],
            Tenders = []
        };

        var results = Validate(request);

        Assert.Contains(results, r => r.MemberNames.Contains(nameof(NovaRetailCreateSaleRequest.Items)));
        Assert.Contains(results, r => r.MemberNames.Contains(nameof(NovaRetailCreateSaleRequest.Tenders)));
    }

    [Fact]
    public void CreateSaleRequest_fails_when_row_numbers_are_duplicated()
    {
        var request = new NovaRetailCreateSaleRequest
        {
            StoreID = 1,
            RegisterID = 1,
            CashierID = 1,
            Items =
            [
                new NovaRetailSaleItemDto { RowNo = 1, UnitPrice = 1000m },
                new NovaRetailSaleItemDto { RowNo = 1, UnitPrice = 1500m }
            ],
            Tenders =
            [
                new NovaRetailSaleTenderDto { RowNo = 2, TenderID = 1, Description = "Efectivo", Amount = 1000m },
                new NovaRetailSaleTenderDto { RowNo = 2, TenderID = 2, Description = "Tarjeta", Amount = 500m }
            ]
        };

        var results = Validate(request);

        Assert.Contains(results, r => r.ErrorMessage?.Contains("RowNo duplicados", StringComparison.OrdinalIgnoreCase) == true);
    }

    [Fact]
    public void Tender_fails_when_amount_is_zero_or_description_is_blank()
    {
        var tender = new NovaRetailSaleTenderDto
        {
            RowNo = 1,
            TenderID = 1,
            Description = " ",
            Amount = 0m
        };

        var results = Validate(tender);

        Assert.Contains(results, r => r.MemberNames.Contains(nameof(NovaRetailSaleTenderDto.Description)));
        Assert.Contains(results, r => r.MemberNames.Contains(nameof(NovaRetailSaleTenderDto.Amount)));
    }

    [Fact]
    public void Item_fails_when_discount_percent_is_out_of_range()
    {
        var item = new NovaRetailSaleItemDto
        {
            RowNo = 1,
            UnitPrice = 1000m,
            LineDiscountPercent = 101m
        };

        var results = Validate(item);

        Assert.Contains(results, r => r.MemberNames.Contains(nameof(NovaRetailSaleItemDto.LineDiscountPercent)));
    }

    [Fact]
    public void Item_fails_when_negative_amounts_are_sent()
    {
        var item = new NovaRetailSaleItemDto
        {
            RowNo = 1,
            UnitPrice = 1000m,
            Cost = -1m,
            Commission = -2m,
            SalesTax = -3m,
            LineDiscountAmount = -4m
        };

        var results = Validate(item);

        Assert.Contains(results, r => r.MemberNames.Contains(nameof(NovaRetailSaleItemDto.Cost)));
        Assert.Contains(results, r => r.MemberNames.Contains(nameof(NovaRetailSaleItemDto.Commission)));
        Assert.Contains(results, r => r.MemberNames.Contains(nameof(NovaRetailSaleItemDto.SalesTax)));
        Assert.Contains(results, r => r.MemberNames.Contains(nameof(NovaRetailSaleItemDto.LineDiscountAmount)));
    }

    [Fact]
    public void Tender_fails_when_surcharges_or_rounding_error_are_negative()
    {
        var tender = new NovaRetailSaleTenderDto
        {
            RowNo = 1,
            TenderID = 1,
            Description = "Tarjeta",
            Amount = 100m,
            RoundingError = -0.01m,
            DebitSurcharge = -0.50m,
            CashBackSurcharge = -1m
        };

        var results = Validate(tender);

        Assert.Contains(results, r => r.MemberNames.Contains(nameof(NovaRetailSaleTenderDto.RoundingError)));
        Assert.Contains(results, r => r.MemberNames.Contains(nameof(NovaRetailSaleTenderDto.DebitSurcharge)));
        Assert.Contains(results, r => r.MemberNames.Contains(nameof(NovaRetailSaleTenderDto.CashBackSurcharge)));
    }

    private static List<ValidationResult> Validate(object instance)
    {
        var context = new ValidationContext(instance);
        var results = new List<ValidationResult>();
        Validator.TryValidateObject(instance, context, results, validateAllProperties: true);
        return results;
    }
}
