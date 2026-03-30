namespace NovaAPI.Models
{
    public class CustomerLookupDto
    {
        public string AccountNumber { get; set; }
        public string FirstName { get; set; }
        public string LastName { get; set; }
        public string PhoneNumber1 { get; set; }
        public string PhoneNumber2 { get; set; }
        public string EmailAddress { get; set; }
        public string Email2 { get; set; }
        public string State { get; set; }
        public string City { get; set; }
        public string City2 { get; set; }
        public string Zip { get; set; }
        public string Address { get; set; }
        public string ActivityCode { get; set; }
        public int? CreditDays { get; set; }
        public int PriceLevel { get; set; }
    }
}
