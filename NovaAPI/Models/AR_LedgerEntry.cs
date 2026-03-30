using System;

namespace NovaAPI.Models
{
    public class AR_LedgerEntry
    {
        public int ID { get; set; }
        public string AppReference { get; set; }
        public int CashierID { get; set; }
        public string LastUpdated { get; set; }
        public int StoreID { get; set; }
        public int LinkType { get; set; }
        public string LinkID { get; set; } // Enviamos AccountNumber para luego obtener el CustomerID de la tabla Customer
        public int DocumentType { get; set; }
        public string PostingDate { get; set; }
        public string DueDate { get; set; }
        public int LedgerType { get; set; }
        public string Description { get; set; }
        public int CurrencyID { get; set; }
        public double CurrencyFactor { get; set; }
        public bool Positive { get; set; }
        public bool Open { get; set; }
        public DateTime? ClosingDate { get; set; }
        public int ReasonID { get; set; }
        public int HoldReasonID { get; set; }
        public int UndoReasonID { get; set; }
        public string Comment { get; set; }
        public int PayMethodID { get; set; }
        public int TransactionID { get; set; }
        public bool IsSync { get; set; }
    }
}