namespace NovaAPI.Models
{
    public class Order
    {
        public int ID { get; set; }
        public int StoreID { get; set; }
        public int Closed { get; set; }
        public string Time { get; set; }
        public int Type { get; set; }
        public string Comment { get; set; }
        public int CustomerID { get; set; }
        public string CustomerFullName { get; set; }
        public string CustomerAccountNumber { get; set; }
        public int ShipToID { get; set; }
        public int DepositOverride { get; set; }
        public decimal Deposit { get; set; }
        public decimal Tax { get; set; }
        public decimal Total { get; set; }
        public string LastUpdated { get; set; }
        public string ExpirationOrDueDate { get; set; }
        public int Taxable { get; set; }
        public int SalesRepID { get; set; }
        public string ReferenceNumber { get; set; }
        public bool CorreoEnviado { get; set; }
        public decimal SubTotal { get; set; }
        public decimal Descuentos { get; set; }
        public bool isSync { get; set; } // isSync = 1 : Está sincronizada. isSync = 0 : No está sincronizada.
        public bool isProforma { get; set; } // Is proforma: 1
        public decimal ImpBonificado { get; set; }
    }
}