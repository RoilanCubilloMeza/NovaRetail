using NovaRetail.Models;

namespace NovaRetail.Tests;

public sealed class LoginUserModelTests
{
    [Fact]
    public void IsAdmin_is_case_insensitive()
    {
        var user = new LoginUserModel
        {
            RoleCode = "admin"
        };

        Assert.True(user.IsAdmin);
    }

    [Fact]
    public void HasRole_matches_role_code_case_insensitively()
    {
        var user = new LoginUserModel
        {
            RoleCode = "Supervisor"
        };

        Assert.True(user.HasRole("supervisor"));
        Assert.False(user.HasRole("cashier"));
    }
}
