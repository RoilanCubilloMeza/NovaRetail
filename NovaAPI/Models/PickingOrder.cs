namespace NovaAPI.Models
{
    public class PickingOrder
    {
        public int ID { get; set; }
        //public string DBTimeStamp { get; set; }
        public int StoreID { get; set; }
        //public int LinkID { get; set; }
        public int RmsID { get; set; }
        public string LastUpdated { get; set; }
        //public int DocType { get; set; }
        //public int DocSource { get; set; }
        //public int DocOption { get; set; }
        public string Number { get; set; }
        public int Status { get; set; }
        //public int DelStatus { get; set; }
        //public int InvStatus { get; set; }
        //public int AllocationID { get; set; }
        public int SupplierID { get; set; }
        public int SupplierTaxID { get; set; }
        public string DateCreated { get; set; }
        public string OrderDate { get; set; }
        public string RequiredDate { get; set; }
        public string DatePlaced { get; set; }
        public int LocationType { get; set; }
        public int LocationID { get; set; }
        public string Reference { get; set; }
        public string AddrTo { get; set; }
        public string ShipTo { get; set; }
        public int PurchaserID { get; set; }
        //public string Requisitioner { get; set; }
        public int ShipViaID { get; set; }
        //public string FOBPoint { get; set; }
        //public string Freight { get; set; }
        public int PayTermID { get; set; }
        //public int CurrencyID { get; set; }
        public double ExchangeRate { get; set; }
        //public int InvDiscMode { get; set; }
        //public decimal InvDiscValue { get; set; }
        public string Comment { get; set; }
        //public string ExternalDocNo { get; set; }
        //public string SupplierDocNo { get; set; }
        //public string SupplierDelNo { get; set; }
        //public string SupplierDelDate { get; set; }
        //public string SupplierInvNo { get; set; }
        //public string SupplierInvDate { get; set; }
        //public decimal SupplierInvAmt { get; set; }
        //public string PostingComment { get; set; }
        public decimal TotalAmount { get; set; }
        public decimal TotalTax { get; set; }
        //public int PoType { get; set; }
        //public string SyncGuid { get; set; }
        public string SupplierName { get; set; }
        public int PhoneNumber { get; set; }
        public string User { get; set; }
    }
}