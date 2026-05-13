using System.DirectoryServices.Protocols;
using System.Net;

namespace SystemeAideBasculement.Services
{
    public sealed class AdLdapService : IAdLdapService
    {
        private readonly string _host;
        private readonly int _port;
        private readonly string _baseDn;
        private readonly string _domainNetbios;
        private readonly string _ccsabGroupDn;
        private readonly ILogger<AuthService> _logger;

        public AdLdapService(IConfiguration cfg, ILogger<AuthService> logger)
        {
            _host = cfg["Ldap:Host"]!;
            _port = int.Parse(cfg["Ldap:Port"] ?? "636");
            _baseDn = cfg["Ldap:BaseDn"]!;
            _domainNetbios = cfg["Ldap:DomainNetbios"]!;
            _ccsabGroupDn = cfg["Ldap:CcsabGroupDn"]!;
            _logger = logger;
        }

        /// <summary>
        /// Valide username/password via LDAPS bind et retourne si l'utilisateur est membre direct de CCSAB.
        /// </summary>
        public (bool Ok, bool IsCcsab, string? DisplayName) AuthenticateAndCheckCcsab(string username, string password)
        {
            using var conn = CreateLdapsConnection(username, password);

            // 1) Bind = validation des identifiants (si erreur => invalid credentials / TLS issue)
            try
            {
                conn.Bind();
            }
            catch
            {
                _logger.LogWarning("[SabUI:AdLdapService]: Invalid credentials or TLS issue for user {Username}", username);
                return (false, false, null);
            }

            // 2) Rechercher l'utilisateur (sAMAccountName)
            var filter = $"(&(objectClass=user)(sAMAccountName={Escape(username)}))";

            var req = new SearchRequest(
                _baseDn,
                filter,
                SearchScope.Subtree,
                "displayName",
                "memberOf"
            );

            try
            {
                var resp = (SearchResponse)conn.SendRequest(req);

                if (resp.Entries.Count != 1)
                {
                    _logger.LogWarning("[SabUI:AdLdapService]: Unable to request User Data for {Username}", username);
                    return (true, false, null);
                }

                var entry = resp.Entries[0];

                // DisplayName (optionnel)
                var displayName = entry.Attributes["displayName"]?.Count > 0
                    ? entry.Attributes["displayName"][0]?.ToString()
                    : null;

                // 3) Vérifier membership direct
                bool isCcsab = false;
                var memberOf = entry.Attributes["memberOf"];
                if (memberOf != null)
                {
                    foreach (var g in memberOf)
                    {
                        if (g?.ToString()?.Equals(_ccsabGroupDn, StringComparison.OrdinalIgnoreCase) == true)
                        {
                            isCcsab = true;
                            break;
                        }
                    }
                }

                return (true, isCcsab, displayName);
            }
            catch
            {
                // Si le serveur ne supporte pas, on continue sans sealing/signing.
                _logger.LogWarning("[SabUI:AdLdapService]: Server does not support sealing/signing for user {Username}", username);
                return (true, false, null);
            }            
        }

        private LdapConnection CreateLdapsConnection(string username, string password)
        {
            var id = new LdapDirectoryIdentifier(_host, _port, fullyQualifiedDnsHostName: true, connectionless: false);

            // DOMAIN\username
            var cred = new NetworkCredential($"{_domainNetbios}\\{username}", password);

            var conn = new LdapConnection(id, cred, AuthType.Negotiate);
            conn.SessionOptions.ProtocolVersion = 3;
            conn.SessionOptions.SecureSocketLayer = true; // LDAPS 636 【2-b3a620】

            // IMPORTANT: ne pas bypasser VerifyServerCertificate en prod.
            // Si la CA interne est trustée sur IIS, ça passe naturellement.
            // conn.SessionOptions.VerifyServerCertificate = (c, cert) => true; // ❌

            return conn;
        }

        private static string Escape(string value)
            => value.Replace("\\", "\\5c")
                    .Replace("*", "\\2a")
                    .Replace("(", "\\28")
                    .Replace(")", "\\29")
                    .Replace("\0", "\\00");
    }
}
