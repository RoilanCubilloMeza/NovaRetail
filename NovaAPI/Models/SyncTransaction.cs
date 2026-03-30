using System.Collections.Generic;

namespace NovaAPI.Models
{
    public class SyncTransaction
    {
        public List<TransactionEntry> transactionEntries { get; set; }
        public List<Transaction> transactions { get; set; }
        public int StoreID { get; set; }

    }
}