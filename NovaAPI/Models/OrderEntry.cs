namespace NovaAPI.Models
{
    public class OrderEntry
    {

        public int ID { get; set; }
        public int StoreID { get; set; }
        public decimal Cost { get; set; }
        public int OrderID { get; set; }
        public int ItemID { get; set; }
        public string ItemLookupCode { get; set; }
        public decimal FullPrice { get; set; }
        public decimal PriceSource { get; set; }
        public decimal Price { get; set; }
        public decimal QuantityOnOrder { get; set; }
        public int SalesRepID { get; set; }
        public int Taxable { get; set; }
        public int DetailID { get; set; }
        public string Description { get; set; }
        public decimal QuantityRTD { get; set; }
        public string LastUpdated { get; set; }
        public string Comment { get; set; }
        public string TransactionTime { get; set; }
        public bool IsBonificado { get; set; }
    }
}