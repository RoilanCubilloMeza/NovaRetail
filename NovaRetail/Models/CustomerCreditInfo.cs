using Newtonsoft.Json;

namespace NovaRetail.Models;

public sealed class CustomerCreditInfo
{
    [JsonProperty("accountNumber")]
    public string AccountNumber { get; set; } = string.Empty;

    [JsonProperty("firstName")]
    public string FirstName { get; set; } = string.Empty;

    [JsonProperty("lastName")]
    public string LastName { get; set; } = string.Empty;

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
}
