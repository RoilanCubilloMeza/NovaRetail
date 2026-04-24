using NovaRetail.Models;

namespace NovaRetail.Services;

internal static class CheckoutCartMapper
{
    public static CartItemModel CloneCartItem(CartItemModel item)
        => new()
        {
            ItemID = item.ItemID,
            SourceOrderEntryID = item.SourceOrderEntryID,
            Emoji = item.Emoji,
            Name = item.Name,
            Code = item.Code,
            UnitPrice = item.UnitPrice,
            UnitPriceColones = item.UnitPriceColones,
            Cost = item.Cost,
            TaxPercentage = item.TaxPercentage,
            TaxID = item.TaxID,
            Cabys = item.Cabys,
            Stock = item.Stock,
            ItemType = item.ItemType,
            OverridePriceColones = item.OverridePriceColones,
            OverrideDescription = item.OverrideDescription,
            DiscountPercent = item.DiscountPercent,
            DiscountReasonCode = item.DiscountReasonCode,
            DiscountReasonCodeID = item.DiscountReasonCodeID,
            ExonerationReasonCodeID = item.ExonerationReasonCodeID,
            ExonerationPercent = item.ExonerationPercent,
            HasExonerationEligibility = item.HasExonerationEligibility,
            IsExonerationEligible = item.IsExonerationEligible,
            SalesRepID = item.SalesRepID,
            SalesRepName = item.SalesRepName,
            IsSelected = item.IsSelected,
            Quantity = item.Quantity
        };

    public static string ResolveMedioPagoCodigo(TenderModel tender)
        => tender.ResolveFiscalMedioPagoCodigo();
}
