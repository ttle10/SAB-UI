

using Microsoft.AspNetCore.Authentication.Cookies;
using Microsoft.AspNetCore.Components.Authorization;
using Microsoft.AspNetCore.Components.Server;
using System.Security.Claims;


namespace SystemeAideBasculement.Services.Security
{
    // Provider serveur : l'état initial (au démarrage du circuit après F5)
    // est fourni par le cookie via HttpContext.User (donc IsAuthenticated sera correct).
    public sealed class SabAuthStateProvider : ServerAuthenticationStateProvider
    {
        public void NotifyLogin(string username, string? displayName, string role)
        {
            var claims = new List<Claim>
            {
                new(ClaimTypes.Name, username),
                new(ClaimTypes.GivenName, string.IsNullOrWhiteSpace(displayName) ? username : displayName),
                new(ClaimTypes.Role, role),
            };

            var identity = new ClaimsIdentity(
                claims,
                CookieAuthenticationDefaults.AuthenticationScheme);

            var principal = new ClaimsPrincipal(identity);

            // Met à jour l'état du circuit Blazor (sans reload)
            SetAuthenticationState(Task.FromResult(new AuthenticationState(principal)));
        }

        public void NotifyLogout()
        {
            SetAuthenticationState(Task.FromResult(
                new AuthenticationState(new ClaimsPrincipal(new ClaimsIdentity()))));
        }
    }
}
