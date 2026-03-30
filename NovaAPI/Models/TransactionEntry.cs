
namespace NovaAPI.Models
{
    public class TransactionEntry
    {
        public int ID { get; set; }
        public int AutoID { get; set; }
        public double Commission { get; set; }
        public double Cost { get; set; }
        public double FullPrice { get; set; }
        public int StoreID { get; set; }
        public int TransactionNumber { get; set; }
        public int ItemID { get; set; }
        public double Price { get; set; }
        public double PriceSource { get; set; }
        public double Quantity { get; set; }
        public int SalesRepID { get; set; }
        public int Taxable { get; set; }
        public int DetailID { get; set; }
        public string Comment { get; set; }
        public int DiscountReasonCodeID { get; set; }
        public int ReturnReasonCodeID { get; set; }
        public int TaxChangeReasonCodeID { get; set; }
        public double SalesTax { get; set; }
        public int QuantityDiscountID { get; set; }
        public int ItemType { get; set; }
        public double ComputedQuantity { get; set; }
        public string TransactionTime { get; set; }
        public double IsAddMoney { get; set; }
        public int VoucherID { get; set; }
        public decimal PrecioEditado { get; set; }

    }
}