using System.Security.Claims;

namespace SystemeAideBasculement.Shared;

public record UserInfo(
    string Username,
    string? DisplayName,
    string? Role
);

public static class UserInfoExtensions
{
    private static readonly SABUnit _sabUnit = new();

    public static UserInfo Empty => new UserInfo(string.Empty, null, null);

    public static UserInfo ToUserInfo(this ClaimsPrincipal principal)
    {
        var username = principal.Identity?.Name ?? "Unknown";
        var displayName = principal.Claims.FirstOrDefault(c => c.Type == ClaimTypes.GivenName)?.Value;
        var role = principal.Claims.FirstOrDefault(c => c.Type == ClaimTypes.Role)?.Value;
        return new UserInfo(username, displayName, role);
    }

    public static bool IsEmpty(this UserInfo userInfo)
    { 
        return string.IsNullOrEmpty(userInfo.Username) &&
               string.IsNullOrEmpty(userInfo.DisplayName) &&
               string.IsNullOrEmpty(userInfo.Role);
    }

    public static bool IsEqual(this UserInfo userInfo1, UserInfo userInfo2)
    {
        if (!string.Equals(userInfo1.Username, userInfo2.Username, StringComparison.OrdinalIgnoreCase)) return false;
        if (!string.Equals(userInfo1.DisplayName, userInfo2.DisplayName, StringComparison.OrdinalIgnoreCase)) return false;
        if (!string.Equals(userInfo1.Role, userInfo2.Role, StringComparison.OrdinalIgnoreCase)) return false;
        return true;
    }

    public static bool IsValidRole(this UserInfo userInfo)
    {
        return _sabUnit.IsValidRole(userInfo.Role);
    }
}