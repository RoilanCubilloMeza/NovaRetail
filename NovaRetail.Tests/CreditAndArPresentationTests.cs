using NovaRetail.Models;

namespace NovaRetail.Tests;

public sealed class CreditAndArPresentationTests
{
    [Fact]
    public void Credit_note_marks_lines_as_negative_for_display()
    {
        var entry = new InvoiceHistoryEntry
        {
            ComprobanteTipo = "04",
            Lines =
            [
                new InvoiceHistoryLine
                {
                    UnitPriceColones = 1500m,
                    LineTotalColones = 3000m
                }
            ]
        };

        entry.ComprobanteTipo = "03";

        Assert.True(entry.IsCreditNote);
        Assert.True(entry.Lines[0].IsCreditNote);
        Assert.StartsWith("CRC -", entry.Lines[0].UnitPriceText, StringComparison.Ordinal);
        Assert.StartsWith("CRC -", entry.Lines[0].LineTotalText, StringComparison.Ordinal);
    }

    [Fact]
    public void Credit_note_shows_applied_source_badge_when_ar_reference_exists()
    {
        var entry = new InvoiceHistoryEntry
        {
            ComprobanteTipo = "03",
            SourceTransactionNumber = 105793,
            AppliedSourceTransactionNumber = 105793
        };

        Assert.True(entry.HasLifecycleBadge);
        Assert.Equal("Aplicada a #105793", entry.LifecycleBadgeText);
    }

    [Fact]
    public void Sale_shows_completed_return_when_credit_note_covers_total()
    {
        var entry = new InvoiceHistoryEntry
        {
            ComprobanteTipo = "01",
            TotalColones = 1615294.97m,
            CreditedAmountColones = 1615294.97m,
            LastAppliedCreditNoteTransactionNumber = 105796
        };

        Assert.True(entry.IsReturnCompleted);
        Assert.False(entry.IsReturnPartial);
        Assert.Equal("Devolucion completa NC #105796", entry.LifecycleBadgeText);
    }

    [Fact]
    public void Sale_shows_partial_return_when_credit_note_does_not_cover_total()
    {
        var entry = new InvoiceHistoryEntry
        {
            ComprobanteTipo = "01",
            TotalColones = 1615294.97m,
            CreditedAmountColones = 1000m,
            LastAppliedCreditNoteTransactionNumber = 105799
        };

        Assert.False(entry.IsReturnCompleted);
        Assert.True(entry.IsReturnPartial);
        Assert.Equal("Devolucion parcial NC #105799", entry.LifecycleBadgeText);
    }

    [Fact]
    public void Credit_note_without_applied_entry_shows_referenced_badge()
    {
        var entry = new InvoiceHistoryEntry
        {
            ComprobanteTipo = "03",
            SourceTransactionNumber = 105793
        };

        Assert.True(entry.HasLifecycleBadge);
        Assert.Equal("Referenciada a #105793", entry.LifecycleBadgeText);
    }

    [Fact]
    public void Empty_document_type_defaults_to_tiquete_electronico()
    {
        var entry = new InvoiceHistoryEntry
        {
            ComprobanteTipo = ""
        };

        Assert.Equal("04", entry.ComprobanteTipo);
        Assert.Equal("Tiquete Electronico", entry.DocumentTypeName);
        Assert.Equal("TE", entry.DocumentIcon);
    }
}
