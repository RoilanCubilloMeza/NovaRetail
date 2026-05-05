using Microsoft.Extensions.DependencyInjection;
using NovaRetail.Models;
using NovaRetail.Pages;
using System.Globalization;

namespace NovaRetail.ViewModels
{
    public partial class MainViewModel
    {
        private async Task OpenStandaloneCreditNoteAsync()
        {
            var clave50 = await _dialogService.PromptAsync(
                "NC por clave",
                "Escanee o ingrese la referencia de compra (Clave 50, consecutivo o número de transacción):",
                "Continuar", "Cancelar",
                placeholder: "Referencia de compra...",
                maxLength: 50);

            if (string.IsNullOrWhiteSpace(clave50))
                return;

            clave50 = clave50.Trim();

            if (!CreditNoteViewModel.IsSupportedStandaloneReference(clave50))
            {
                await _dialogService.AlertAsync(
                    "NC por clave",
                    "La referencia debe ser una clave 50, un consecutivo o un numero de transaccion valido.",
                    "OK");
                return;
            }

            InvoiceHistoryEntry? foundEntry = null;
            if (ShouldSearchInvoiceReferenceRemotely(clave50))
            {
                try
                {
                    var result = await _saleService.SearchInvoiceHistoryAsync(clave50, CancellationToken.None);
                    if (result.Ok && result.Entries.Count > 0)
                    {
                        var match = result.Entries.FirstOrDefault(e =>
                            string.Equals(e.Clave50?.Trim(), clave50, StringComparison.OrdinalIgnoreCase) ||
                            string.Equals(e.Consecutivo?.Trim(), clave50, StringComparison.OrdinalIgnoreCase) ||
                            string.Equals(e.TransactionNumber.ToString(CultureInfo.InvariantCulture), clave50, StringComparison.OrdinalIgnoreCase));

                        if (match is not null)
                        {
                            foundEntry = MapRemoteEntry(match);
                            foundEntry = await EnsureStandaloneCreditNoteDetailAsync(foundEntry);
                        }
                    }
                }
                catch
                {
                }
            }

            var page = Application.Current?.Handler?.MauiContext?.Services.GetService<CreditNotePage>();
            if (page is null)
                return;

            if (foundEntry is not null && foundEntry.Lines.Count > 0)
                await page.LoadAsync(foundEntry);
            else
                await page.LoadStandaloneAsync(clave50);

            await Shell.Current.Navigation.PushAsync(page);
        }

        private static bool ShouldSearchInvoiceReferenceRemotely(string reference)
        {
            var value = (reference ?? string.Empty).Trim();
            if (value.Length == 0)
                return false;

            if (value.All(char.IsDigit))
                return value.Length is >= 5 and <= 6;

            return value.Length is 20 or 50;
        }

        private async Task<InvoiceHistoryEntry> EnsureStandaloneCreditNoteDetailAsync(InvoiceHistoryEntry entry)
        {
            if (entry.IsLocalEntry || entry.Lines.Count > 0)
                return entry;

            var result = await _saleService.GetInvoiceHistoryDetailAsync(entry.TransactionNumber);
            if (!result.Ok || result.Entry is null)
                return entry;

            var detailedEntry = MapRemoteEntry(result.Entry);
            detailedEntry.SourceTransactionNumber = entry.SourceTransactionNumber;
            detailedEntry.AppliedSourceTransactionNumber = entry.AppliedSourceTransactionNumber;
            detailedEntry.CreditedAmountColones = entry.CreditedAmountColones;
            return detailedEntry;
        }

        private static InvoiceHistoryEntry MapRemoteEntry(NovaRetailInvoiceHistoryEntryDto entry)
        {
            return new InvoiceHistoryEntry
            {
                IsLocalEntry = false,
                Date = entry.Date,
                TransactionNumber = entry.TransactionNumber,
                ComprobanteTipo = string.IsNullOrWhiteSpace(entry.ComprobanteTipo) ? "04" : entry.ComprobanteTipo,
                Clave50 = entry.Clave50 ?? string.Empty,
                Consecutivo = entry.Consecutivo ?? string.Empty,
                ClientId = entry.ClientId ?? string.Empty,
                ClientName = string.IsNullOrWhiteSpace(entry.ClientName) ? "CLIENTE CONTADO" : entry.ClientName,
                CreditAccountNumber = entry.CreditAccountNumber ?? string.Empty,
                CashierName = entry.CashierName ?? string.Empty,
                RegisterNumber = entry.RegisterNumber,
                StoreName = entry.StoreName ?? string.Empty,
                SubtotalColones = entry.SubtotalColones,
                DiscountColones = entry.DiscountColones,
                ExonerationColones = entry.ExonerationColones,
                TaxColones = entry.TaxColones,
                TotalColones = entry.TotalColones,
                ChangeColones = entry.ChangeColones,
                TenderDescription = entry.TenderDescription ?? string.Empty,
                TenderTotalColones = entry.TenderTotalColones,
                SecondTenderDescription = entry.SecondTenderDescription ?? string.Empty,
                SecondTenderAmountColones = entry.SecondTenderAmountColones,
                Lines = entry.Lines.Select((line, index) => new InvoiceHistoryLine
                {
                    LineNumber = line.LineNumber > 0 ? line.LineNumber : index + 1,
                    ItemID = line.ItemID,
                    TaxID = line.TaxID,
                    DisplayName = line.DisplayName ?? string.Empty,
                    Code = line.Code ?? string.Empty,
                    Quantity = line.Quantity,
                    TaxPercentage = line.TaxPercentage,
                    UnitPriceColones = line.UnitPriceColones,
                    LineTotalColones = line.LineTotalColones,
                    HasDiscount = line.HasDiscount,
                    DiscountPercent = line.DiscountPercent,
                    HasExoneration = line.HasExoneration,
                    ExonerationPercent = line.ExonerationPercent,
                    HasOverridePrice = line.HasOverridePrice
                }).OrderBy(line => line.LineNumber).ToList()
            };
        }
    }
}
