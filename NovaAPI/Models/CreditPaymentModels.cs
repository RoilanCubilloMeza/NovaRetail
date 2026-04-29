using System.Collections.Generic;

namespace NovaAPI.Models
{
    public class CreditPaymentRequest
    {
        public string AccountNumber { get; set; }
        public decimal Amount { get; set; }
        public int CashierID { get; set; }
        public int StoreID { get; set; }
        public int TenderID { get; set; }
        public string Comment { get; set; }
        public string Reference { get; set; }
        public List<PaymentApplicationItem> Applications { get; set; }
    }

    public class PaymentApplicationItem
    {
        public int LedgerEntryID { get; set; }
        public decimal Amount { get; set; }
        public decimal EntryBalance { get; set; }
    }

    public class CreditPaymentResponse
    {
        public bool Ok { get; set; }
        public string Message { get; set; }
        public bool RmhposOk { get; set; }
        public bool AppCentralOk { get; set; }
        public string AppCentralMessage { get; set; }
        public int PaymentID { get; set; }
        public string Reference { get; set; }
    }

    public class OpenLedgerEntryDto
    {
        public int LedgerEntryID { get; set; }
        public string PostingDate { get; set; }
        public string DueDate { get; set; }
        public string LedgerTypeName { get; set; }
        public string DocumentTypeName { get; set; }
        public string Description { get; set; }
        public int StoreID { get; set; }
        public string Reference { get; set; }
        public decimal Amount { get; set; }
        public decimal Balance { get; set; }
    }
}
