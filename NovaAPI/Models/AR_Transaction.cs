namespace NovaAPI.Models
{
    public class AR_Transaction
    {
        public int ID { get; set; }
        public int StoreID { get; set; }
        public int UserID { get; set; }
        public string PostingDate { get; set; }
        public string CustomerID { get; set; } //Enviamos AccountNumber, para luego encontrar el CustomerID mediante ese valor
        public int OrderID { get; set; }
        public string AppReference { get; set; }
        public int DocumentType { get; set; }
        public decimal Amount { get; set; }
        public decimal Balance { get; set; }
        public int CashierID { get; set; }
        public int TenderID { get; set; }
        public int ReceivableID { get; set; }
        public int Status { get; set; }
        public string PostedDate { get; set; }
        public bool IsSync { get; set; }
    }
}