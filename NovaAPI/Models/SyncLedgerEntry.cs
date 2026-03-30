using System.Collections.Generic;

namespace NovaAPI.Models
{
    public class SyncLedgerEntry
    {
        public List<AR_LedgerEntry> LedgerEntries { get; set; }
        public List<AR_LedgerEntryDetail> LedgerEntryDetails { get; set; }
    }
}