// Services/Security/RevalidatingAuthenticationStateProvider.cs
using Microsoft.AspNetCore.Authentication.Cookies;
using Microsoft.AspNetCore.Components.Authorization;
using Microsoft.AspNetCore.Components.Server;
using System.Security.Claims;


namespace SystemeAideBasculement.Services.Security
{
    public class RevalidatingAuthenticationStateProvider
        : RevalidatingServerAuthenticationStateProvider
    {
        private readonly IHttpContextAccessor _httpContextAccessor;


        public RevalidatingAuthenticationStateProvider(
                   ILoggerFactory loggerFactory,
                   IHttpContextAccessor httpContextAccessor)
                   : base(loggerFactory)
        {
            _httpContextAccessor = httpContextAccessor;

            // Seed auth state from HttpContext on circuit start
            // This is what survives F5 — the cookie is in HttpContext
            var httpContext = _httpContextAccessor.HttpContext;
            if (httpContext?.User.Identity?.IsAuthenticated == true)
            {
                var authState = new AuthenticationState(httpContext.User);
                SetAuthenticationState(Task.FromResult(authState));
            }
        }

        // Re-check the cookie every 30 minutes while the circuit is open
        protected override TimeSpan RevalidationInterval => TimeSpan.FromMinutes(30);

        protected override Task<bool> ValidateAuthenticationStateAsync(
            AuthenticationState authenticationState,
            CancellationToken cancellationToken)
        {
            var user = authenticationState.User;
            var isValid = user.Identity?.IsAuthenticated == true
                       && user.FindFirst(ClaimTypes.Name) != null;

            return Task.FromResult(isValid);
        }
    }
}