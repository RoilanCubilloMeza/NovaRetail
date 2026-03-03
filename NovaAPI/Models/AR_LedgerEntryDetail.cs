namespace NovaAPI.Models
{
    public class AR_LedgerEntryDetail
    {
        public int ID { get; set; }
        public int StoreID { get; set; }
        public string AppReference { get; set; }
        public int CashierID { get; set; }
        public int LedgerEntryID { get; set; }
        public int LedgerType { get; set; }
        public string DueDate { get; set; }
        public string PostingDate { get; set; }
        public int DetailType { get; set; }
        /* public string Reference { get; set; }*/
        public decimal Amount { get; set; }
        public decimal AmountLCY { get; set; }
        public decimal AmountACY { get; set; }
        public int AppliedEntryID { get; set; }
        public decimal AppliedAmount { get; set; }
        public int UnapplyEntryID { get; set; }
        public int UnapplyReasonID { get; set; }
        public bool IsSync { get; set; }
    }
}