using System.Security.Claims;

namespace Library.Web.Infrastructure;

public static class Auth
{
    public const string Staff = "Staff";
    public const string Patron = "Patron";
    public const string Kiosk = "Kiosk";

    public const string Admin = "Admin";
    public const string Reception = "Reception";
    public const string Manager = "Manager";
    public const string AdminOrReception = "Admin,Reception";
    public const string AdminOrManager = "Admin,Manager";
    public const string AnyStaff = "Admin,Reception,Manager";

    public static int GetUserId(this ClaimsPrincipal user) =>
        int.TryParse(user.FindFirstValue(ClaimTypes.NameIdentifier), out var id) ? id : 0;

    public static ClaimsPrincipal Identity(string scheme, int id, string name, string? role = null)
    {
        var claims = new List<Claim> { new(ClaimTypes.NameIdentifier, id.ToString()), new(ClaimTypes.Name, name) };
        if (role != null) claims.Add(new Claim(ClaimTypes.Role, role));
        return new ClaimsPrincipal(new ClaimsIdentity(claims, scheme));
    }
}
