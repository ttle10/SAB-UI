namespace SystemeAideBasculement.Services.Security
{
    /// <summary>
    /// Dev-only mock — bypasses real LDAP.
    /// Configure test users in appsettings.Development.json under "DevAuth:Users".
    /// </summary>
    public sealed class MockAdLdapService : IAdLdapService
    {
        private readonly ILogger<MockAdLdapService> _logger;
        private readonly List<DevUser> _users;

        public MockAdLdapService(
            IConfiguration cfg,
            ILogger<MockAdLdapService> logger)
        {
            _logger = logger;
            _users = cfg
                .GetSection("DevAuth:Users")
                .Get<List<DevUser>>() ?? new List<DevUser>();
        }

        public AuthResult Authenticate(string username, string password)
        {
            _logger.LogWarning(
                "[SabUI:MockAdLdapService]: DEV MODE — LDAP bypassed for user '{Username}'",
                username);

            var user = _users.FirstOrDefault(u =>
                string.Equals(u.Username, username, StringComparison.OrdinalIgnoreCase) &&
                u.Password == password);

            if (user is null)
                return new AuthResult { IsAuthenticated = false };

            return new AuthResult
            {
                IsAuthenticated = true,
                Unit = user.IsCcsab ? new SABUnit() : new DefaultUnit(),
                UserDisplayName = user.DisplayName
            };
        }

        private class DevUser
        {
            public string Username { get; set; } = "";
            public string Password { get; set; } = "";
            public bool IsCcsab { get; set; }
            public string? DisplayName { get; set; }
        }
    }
}
