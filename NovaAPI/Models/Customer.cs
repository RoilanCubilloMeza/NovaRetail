namespace NovaAPI.Models
{
    public class Customer
    {
        public int ID { get; set; }

        public int AccountTypeID { get; set; }

        public string AccountNumber { get; set; }

        public string FirstName { get; set; }

        public string LastName { get; set; }

        public string PhoneNumber1 { get; set; }

        public string PhoneNumber2 { get; set; }

        public string EmailAddress { get; set; }

        public string State { get; set; }

        public string City { get; set; }

        public string City2 { get; set; }

        public string Zip { get; set; }

        public string Address { get; set; }

        public string ActivityCode { get; set; }

        public int? CreditDays { get; set; }

        public string Source { get; set; }

        public string LastUpdated { get; set; }

        public string Vendedor { get; set; }

        public decimal? ClosingBalance { get; set; }

        public decimal? CreditLimit { get; set; }

        public decimal? Available { get; set; }
    }
}