namespace NovaRetail.Models;

public class CustomerLookupModel
{
    public int CustomerId { get; set; }
    public string AccountNumber { get; set; } = string.Empty;
    public string TaxNumber { get; set; } = string.Empty;
    public string FirstName { get; set; } = string.Empty;
    public string LastName { get; set; } = string.Empty;
    public string Phone { get; set; } = string.Empty;
    public string Email { get; set; } = string.Empty;
    public string Address { get; set; } = string.Empty;
    public string City { get; set; } = string.Empty;
    public string State { get; set; } = string.Empty;
    public string Zip { get; set; } = string.Empty;
    public int AccountTypeID { get; set; }
    public bool IsEven { get; set; }

    public string ResolvedClientId => !string.IsNullOrWhiteSpace(TaxNumber)
        ? TaxNumber.Trim()
        : AccountNumber.Trim();

    public string FullName => string.Join(" ",
        new[] { FirstName, LastName }.Where(x => !string.IsNullOrWhiteSpace(x)));

    public string DisplayName => !string.IsNullOrWhiteSpace(FullName) ? FullName : "(Sin nombre)";
    public string SearchCodeText => !string.IsNullOrWhiteSpace(AccountNumber)
        ? AccountNumber.Trim()
        : ResolvedClientId;
    public string SearchSummaryText
    {
        get
        {
            var parts = new List<string>();
            if (!string.IsNullOrWhiteSpace(TaxNumber))
                parts.Add($"Cédula {TaxNumber.Trim()}");
            if (!string.IsNullOrWhiteSpace(Phone))
                parts.Add(Phone.Trim());
            return string.Join(" • ", parts);
        }
    }
}
