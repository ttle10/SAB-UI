using System.Security.Claims;

namespace SystemeAideBasculement.Shared;

public record UserInfo(
    string Username,
    string? DisplayName,
    string? Role
);

public static class UserInfoExtensions
{
    public static UserInfo ToUserInfo(this ClaimsPrincipal principal)
    {
        var username = principal.Identity?.Name ?? "Unknown";
        var displayName = principal.Claims.FirstOrDefault(c => c.Type == ClaimTypes.GivenName)?.Value;
        var role = principal.Claims.FirstOrDefault(c => c.Type == ClaimTypes.Role)?.Value;
        return new UserInfo(username, displayName, role);
    }
}