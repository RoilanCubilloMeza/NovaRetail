using Newtonsoft.Json;

namespace NovaRetail.Models;

public sealed class CustomerCreditInfo
{
    [JsonProperty("id")]
    public int ID { get; set; }

    [JsonProperty("accountNumber")]
    public string AccountNumber { get; set; } = string.Empty;

    [JsonProperty("firstName")]
    public string FirstName { get; set; } = string.Empty;

    [JsonProperty("lastName")]
    public string LastName { get; set; } = string.Empty;

    [JsonProperty("accountTypeID")]
    public int AccountTypeID { get; set; }

    [JsonProperty("creditDays")]
    public int? CreditDays { get; set; }

    [JsonProperty("closingBalance")]
    public decimal ClosingBalance { get; set; }

    [JsonProperty("creditLimit")]
    public decimal CreditLimit { get; set; }

    [JsonProperty("available")]
    public decimal Available { get; set; }

    [JsonProperty("hasCredit")]
    public bool HasCredit { get; set; }

    public string FullName => $"{FirstName} {LastName}".Trim();

    public bool IsEven { get; set; }
}
