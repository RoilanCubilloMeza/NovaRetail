namespace NovaRetail.Models;

public class CustomerLookupModel
{
    public string AccountNumber { get; set; } = string.Empty;
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

    public string FullName => string.Join(" ",
        new[] { FirstName, LastName }.Where(x => !string.IsNullOrWhiteSpace(x)));

    public string DisplayName => !string.IsNullOrWhiteSpace(FullName) ? FullName : "(Sin nombre)";
}
