namespace NovaAPI.Models
{
    public class Payment
    {
        public int ID { get; set; }
        public int CashierID { get; set; }
        public int StoreID { get; set; }
        public string CustomerID { get; set; } //Enviamos el AccountNumber para luego obtener el ID mediante ese valor
        public string Time { get; set; }
        public decimal Amount { get; set; }
        public string Comment { get; set; }
        public string AppReference { get; set; }
        public bool IsSync { get; set; }
    }
}