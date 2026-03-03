namespace NovaAPI.Models
{
    public class Transaction
    {
        public int TransactionNumber { get; set; }
        public int AutoID { get; set; }
        public int ShipToID { get; set; }
        public int StoreID { get; set; }
        public int BatchNumber { get; set; }
        public string Time { get; set; }
        public int CustomerID { get; set; }
        public string CustomerFullName { get; set; }
        public string CustomerAccountNumber { get; set; }
        public int CashierID { get; set; }
        public double Total { get; set; }
        public double SalesTax { get; set; }
        public string Comment { get; set; }
        public string ReferenceNumber { get; set; }
        public int Status { get; set; }
        public int ExchangeID { get; set; }
        public int ChannelType { get; set; }
        public int RecallID { get; set; }
        public int RecallType { get; set; }
        public int SalesRepID { get; set; }
        public decimal SubTotal { get; set; }
        public decimal Descuentos { get; set; }


    }
}