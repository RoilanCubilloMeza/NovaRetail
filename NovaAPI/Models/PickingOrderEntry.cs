namespace NovaAPI.Models
{
    public class PickingOrderEntry
    {
        public int ID { get; set; }
        //public string DBTimeStamp { get; set; }
        public int StoreID { get; set; }
        //public int LinkID { get; set; }
        //public int RmsID { get; set; }
        public string LastUpdated { get; set; }
        public int OrderID { get; set; }
        public int LineType { get; set; }
        public int LineNumber { get; set; }
        public int EntryType { get; set; }
        public int EntryID { get; set; }
        public int ItemTaxID { get; set; }
        public string OrderNumber { get; set; }
        public string Description { get; set; }
        public int UOMID { get; set; }
        public decimal Quantity { get; set; }
        public decimal QtyReceived { get; set; }
        //public decimal QtyToReceive { get; set; }
        //public decimal QtyInvoiced { get; set; }
        //public decimal QtytoInvoice { get; set; }
        //public decimal QtyPerUOM { get; set; }
        //public decimal QtyPerInvoice { get;set; }
        public decimal UnitCost { get; set; }
        //public decimal LineDiscRate { get;set; }
        //public decimal TaxRate { get;set; }
        //public int LinkedEntryID { get; set; }
        //public int ParentEntryID { get; set; }
        //public int InventoryOflineID { get; set; }
        public string Comment { get; set; }
    }
}