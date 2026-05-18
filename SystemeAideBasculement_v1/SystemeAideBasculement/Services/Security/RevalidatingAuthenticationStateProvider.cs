// Services/Security/RevalidatingAuthenticationStateProvider.cs
using Microsoft.AspNetCore.Authentication.Cookies;
using Microsoft.AspNetCore.Components.Authorization;
using Microsoft.AspNetCore.Components.Server;
using Microsoft.AspNetCore.Identity;
using System.Security.Claims;


namespace SystemeAideBasculement.Services.Security
{
    public class RevalidatingAuthenticationStateProvider
        : RevalidatingServerAuthenticationStateProvider
    {
        private readonly IServiceScopeFactory _scopeFactory;

        public RevalidatingAuthenticationStateProvider(
            ILoggerFactory loggerFactory,
            IServiceScopeFactory scopeFactory)
            : base(loggerFactory)
        {
            _scopeFactory = scopeFactory;
        }

        // Re-check the cookie every 30 minutes while the circuit is open
        protected override TimeSpan RevalidationInterval => TimeSpan.FromMinutes(30);

        protected override async Task<bool> ValidateAuthenticationStateAsync(
            AuthenticationState authenticationState,
            CancellationToken cancellationToken)
        {
            // The cookie is still valid if the principal has the Name claim
            var user = authenticationState.User;
            return await Task.FromResult(
                user.Identity?.IsAuthenticated == true &&
                user.FindFirst(ClaimTypes.Name) != null);
        }
    }
}