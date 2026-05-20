using System.Security.Claims;
using SystemeAideBasculement.Shared;

namespace SystemeAideBasculement.Services.Security
{
    public interface IUserContextService
    {
        UserInfo? GetUserInfo(ClaimsPrincipal user);
    }

    public class UserContextService : IUserContextService
    {
        public UserInfo? GetUserInfo(ClaimsPrincipal user)
        {
            if (user.Identity?.IsAuthenticated != true)
                return null;

            return new UserInfo(
                Username: user.Identity.Name ?? "Anonymous",
                DisplayName: user.FindFirst(ClaimTypes.GivenName)?.Value,
                Role: user.FindFirst(ClaimTypes.Role)?.Value
            );
        }
    }
}
