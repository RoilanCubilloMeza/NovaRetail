namespace NovaAPI.Models
{
    public class CustomerCreditInfoDto
    {
        public int ID { get; set; }
        public string AccountNumber { get; set; }
        public string FirstName { get; set; }
        public string LastName { get; set; }
        public int AccountTypeID { get; set; }
        public int? CreditDays { get; set; }
        public decimal ClosingBalance { get; set; }
        public decimal CreditLimit { get; set; }
        public decimal Available { get; set; }
        public bool HasCredit { get; set; }
    }
}
