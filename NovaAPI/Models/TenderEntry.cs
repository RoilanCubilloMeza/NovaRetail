namespace NovaAPI.Models
{
    public class TenderEntry
    {
        public int ID { get; set; }
        public string CreditCardExpiration { get; set; }
        public int OrderHistoryID { get; set; }
        public int DropPayoutID { get; set; }
        public int StoreID { get; set; }
        public int TransactionNumber { get; set; }
        public string TenderCode { get; set; }
        public string AppReference { get; set; }
        public int CashierID { get; set; }
        public int PaymentID { get; set; }
        public string Description { get; set; }
        public string CreditCardNumber { get; set; }
        public string CreditCardApprovalCode { get; set; }
        public decimal Amount { get; set; }
        public string AccountHolder { get; set; }
        public decimal RoundingError { get; set; }
        public decimal AmountForeign { get; set; }
        public string BankNumber { get; set; }
        public string SerialNumber { get; set; }
        public string State { get; set; }
        public string License { get; set; }
        public string TransitNumber { get; set; }
        public int VisaNetAuthorizationID { get; set; }
        public decimal DebitSurcharge { get; set; }
        public decimal CashBackSurcharge { get; set; }
        public bool IsCreateNew { get; set; }
        public bool IsSync { get; set; }
    }
}