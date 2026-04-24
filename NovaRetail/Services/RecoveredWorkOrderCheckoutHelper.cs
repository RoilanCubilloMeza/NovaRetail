using NovaRetail.Models;
using System;
using System.Collections.Generic;
using System.Linq;

namespace NovaRetail.Services;

internal static class RecoveredWorkOrderCheckoutHelper
{
    public static List<NovaRetailQuoteItemRequest> BuildRemainingItems(
        NovaRetailOrderDetail? detail,
        IReadOnlyCollection<CartItemModel> soldItems)
    {
        if (detail is null || detail.Entries.Count == 0)
            return new List<NovaRetailQuoteItemRequest>();

        var entryLookup = detail.Entries.ToDictionary(entry => entry.EntryID);
        var soldByEntry = new Dictionary<int, decimal>();

        foreach (var item in soldItems)
        {
            if (item.SourceOrderEntryID <= 0)
                continue;

            if (!entryLookup.TryGetValue(item.SourceOrderEntryID, out var sourceEntry))
                continue;

            var sourceQuantity = sourceEntry.QuantityOnOrder > 0m ? sourceEntry.QuantityOnOrder : 1m;
            var soldQuantity = Math.Min(Math.Max(item.Quantity, 0m), sourceQuantity);
            if (soldQuantity <= 0m)
                continue;

            soldByEntry.TryGetValue(item.SourceOrderEntryID, out var currentSold);
            soldByEntry[item.SourceOrderEntryID] = Math.Min(sourceQuantity, currentSold + soldQuantity);
        }

        var remainingItems = new List<NovaRetailQuoteItemRequest>(detail.Entries.Count);
        foreach (var entry in detail.Entries)
        {
            var sourceQuantity = entry.QuantityOnOrder > 0m ? entry.QuantityOnOrder : 1m;
            soldByEntry.TryGetValue(entry.EntryID, out var soldQuantity);
            var remainingQuantity = Math.Round(sourceQuantity - soldQuantity, 4);
            if (remainingQuantity <= 0m)
                continue;

            remainingItems.Add(new NovaRetailQuoteItemRequest
            {
                ItemID = entry.ItemID,
                Cost = entry.Cost,
                FullPrice = entry.FullPrice,
                PriceSource = entry.PriceSource,
                Price = entry.Price,
                QuantityOnOrder = remainingQuantity,
                SalesRepID = entry.SalesRepID,
                Taxable = entry.Taxable,
                DetailID = entry.DetailID,
                Description = entry.Description,
                Comment = entry.Comment,
                DiscountReasonCodeID = entry.DiscountReasonCodeID,
                ReturnReasonCodeID = entry.ReturnReasonCodeID,
                TaxChangeReasonCodeID = entry.TaxChangeReasonCodeID
            });
        }

        return remainingItems;
    }

    public static (decimal Tax, decimal Total) CalculateTotals(
        IEnumerable<NovaRetailQuoteItemRequest> items,
        decimal defaultTaxPercentage,
        bool isTaxIncluded)
    {
        decimal tax = 0m;
        decimal total = 0m;
        var taxRate = defaultTaxPercentage / 100m;

        foreach (var item in items)
        {
            if (item is null)
                continue;

            var quantity = item.QuantityOnOrder > 0m ? item.QuantityOnOrder : 1m;
            var lineTotal = Math.Round(item.Price * quantity, 4);
            total += lineTotal;

            if (!item.Taxable || taxRate <= 0m)
                continue;

            tax += isTaxIncluded
                ? lineTotal - (lineTotal / (1m + taxRate))
                : lineTotal * taxRate;
        }

        return (Math.Round(tax, 4), Math.Round(total, 4));
    }
}
