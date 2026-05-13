using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authentication.Cookies;
using Microsoft.AspNetCore.Identity.Data;
using System.Security.Claims;

namespace SystemeAideBasculement.Services
{
    public record AuthRequest(string Username, string Password);

    public class AuthService
    {
        private readonly IAdLdapService _ldap;
        private readonly ILogger<AuthService> _logger;

        public AuthService(IAdLdapService ldap, ILogger<AuthService> logger)
        {
            _ldap = ldap;
            _logger = logger;
        }

        public async Task<IResult> LoginAsync(HttpContext http, AuthRequest req)
        {
            if (string.IsNullOrWhiteSpace(req.Username) ||
                string.IsNullOrWhiteSpace(req.Password))
                return Results.BadRequest(new { error = "Identifiants manquants." });

            try
            {
                var (ok, isCcsab, displayName) = await Task.Run(() =>
                    _ldap.AuthenticateAndCheckCcsab(req.Username, req.Password));

                if (!ok)
                    return Results.Json(new { error = "Nom d'utilisateur ou mot de passe invalide." }, statusCode: 401);

                if (!isCcsab)
                    return Results.Json(new { error = "Accès refusé." }, statusCode:403);

                var name = string.IsNullOrWhiteSpace(displayName) ? req.Username : displayName;

                var claims = new List<Claim>
            {
                new(ClaimTypes.Name, req.Username),
                new(ClaimTypes.GivenName, name),
                new("group", "CCSAB")
            };

                var identity = new ClaimsIdentity(
                    claims, CookieAuthenticationDefaults.AuthenticationScheme);

                await http.SignInAsync(
                    CookieAuthenticationDefaults.AuthenticationScheme,
                    new ClaimsPrincipal(identity),
                    new AuthenticationProperties
                    {
                        IsPersistent = true,
                        ExpiresUtc = DateTimeOffset.UtcNow.AddHours(8)
                    });

                _logger.LogInformation("User '{Username}' authenticated.", req.Username);

                return Results.Ok(new { displayName = name });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "LDAP error for '{Username}'", req.Username);
                return Results.Json(
                    new { error = "Erreur de connexion au service d'authentification." },
                    statusCode: 500);
            }
        }
    }
}