using Microsoft.Extensions.Options;
using System.DirectoryServices.Protocols;
using System.Net;
using System.Text;
using SystemeAideBasculement.Shared;

namespace SystemeAideBasculement.Services.Security
{
    public sealed class AdLdapService : IAdLdapService
    {
        private readonly LdapOptions _options;
        private readonly ILogger<AuthService> _logger;

        public AdLdapService(IOptions<LdapOptions> opt, ILogger<AuthService> logger)
        {
            _options = opt.Value;
            _logger = logger;
        }

        /// <summary>
        /// Valide username/password via LDAPS bind et retourne si l'utilisateur est membre direct de CCSAB.
        /// </summary>
        public AuthResult Authenticate(string username, string password)
        {
            using var conn = CreateLdapsConnection(username, password);
            bool isAuthenticated = false;

            _logger.LogInformation("[SabUI:AdLdapService]: Attempting to authenticate user {Username}", username);
            // 1) Bind = validation des identifiants (si erreur => invalid credentials / TLS issue)
            try
            {
                conn.Bind();
                isAuthenticated = true;
            }
            catch(Exception ex)
            {
                _logger.LogWarning(ex,  "[SabUI:AdLdapService]: Invalid credentials or TLS issue for user {Username}", username);
                return new AuthResult { IsAuthenticated = isAuthenticated };
            }

            _logger.LogInformation("[SabUI:AdLdapService]: Successfully authenticated user {Username}", username);
            // 2) Rechercher l'utilisateur (sAMAccountName)
            var filter = $"(&(objectClass=user)(sAMAccountName={Escape(username)}))";

            var req = new SearchRequest(
                _options.BaseDn,
                filter,
                SearchScope.Subtree,
                "displayName",
                "memberOf"
            );

            try
            {
                // 1. récupérer DN + displayName utilisateur
                var userReq = new SearchRequest(
                    _options.BaseDn,
                    $"(&(objectClass=user)(sAMAccountName={username}))",
                    SearchScope.Subtree,
                    "distinguishedName",
                    "displayName");

                var userResp = (SearchResponse)conn.SendRequest(userReq);

                if (userResp.Entries.Count != 1)
                {
                    _logger.LogWarning("[SabUI:AdLdapService]: User not found or multiple entries: {Username}", username);
                    return new AuthResult { IsAuthenticated = isAuthenticated };
                }

                var entry = userResp.Entries[0];

                // DN utilisateur
                var userDn = entry.Attributes["distinguishedName"][0].ToString();

                // displayName (optionnel)
                string? displayName = entry.Attributes["displayName"]?[0]?.ToString();

                // 2. vérifier membership dans CCSAB
                var groupReq = new SearchRequest(
                    _options.CcsabGroupDn,
                    $"(member={userDn})",
                    SearchScope.Base,
                    "member");

                var groupResp = (SearchResponse)conn.SendRequest(groupReq);

                _logger.LogTrace("[SabUI:AdLdapService]:User Group found : {Count}", groupResp.Entries.Count);

                bool isCcsab = false;
                if (groupResp.Entries.Count > 0)
                {
                    var membersAttr = groupResp.Entries[0].Attributes["member"];

                    if (membersAttr == null)
                    {
                        _logger.LogWarning("[SabUI:AdLdapService]: Attribute 'member' is NULL (no permission or empty group)");
                    }
                    else
                    {
                        foreach (var m in membersAttr)
                        {
                            if (m == null)
                            {
                                _logger.LogWarning("[SabUI:AdLdapService]: Null member entry detected");
                                continue;
                            }

                            string memberDn;

                            if (m is byte[] bytes)
                            {
                                memberDn = System.Text.Encoding.UTF8.GetString(bytes);
                            }
                            else
                            {
                                memberDn = m?.ToString() ?? string.Empty;
                                if (string.IsNullOrEmpty(memberDn))
                                {
                                    _logger.LogWarning("[SabUI:AdLdapService]: Null memberDn entry detected");
                                }
                            }

                            _logger.LogTrace("[SabUI:AdLdapService]:Compare GROUP MEMBER: {MemberDn} to USER DN: {UserDn}", memberDn, userDn);

                            if (!string.IsNullOrWhiteSpace(memberDn) &&
                                string.Equals(memberDn.Trim(), userDn?.Trim(), StringComparison.OrdinalIgnoreCase))
                            {
                                _logger.LogTrace("[SabUI:AdLdapService]: MATCH FOUND");
                                isCcsab = true;
                                break;
                            }
                        }
                    }
                }

                if (!isCcsab)
                {
                    _logger.LogWarning("[SabUI:AdLdapService]: User NOT in CCSAB");
                }


                return new AuthResult
                {
                    IsAuthenticated = isAuthenticated,
                    UserDisplayName = displayName,
                    Unit = isCcsab ? new SABUnit() : new DefaultUnit()
                };
            }
            catch (LdapException ex)
            {
                _logger.LogWarning(ex, "[SabUI:AdLdapService]: Auth failed for {User}", username);
                return new AuthResult { IsAuthenticated = isAuthenticated };
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "[SabUI:AdLdapService]: Unexpected error for {User}", username);
                return new AuthResult { IsAuthenticated = false };
            }
        }

        private LdapConnection CreateLdapsConnection(string username, string password)
        {
            var id = new LdapDirectoryIdentifier(_options.Host, _options.Port, fullyQualifiedDnsHostName: false, connectionless: false);

            // DOMAIN\username
            //var cred = new NetworkCredential($"{_domainNetbios}\\{username}", password);
            var cred = new NetworkCredential(username, password, _options.DomainNetbios);
            var conn = new LdapConnection(id, cred, AuthType.Negotiate);
            conn.SessionOptions.ProtocolVersion = 3;
            conn.SessionOptions.SecureSocketLayer = true; // LDAPS 636 【2-b3a620】

            // IMPORTANT: ne pas bypasser VerifyServerCertificate en prod.
            // Si la CA interne est trustée sur IIS, ça passe naturellement.
            conn.SessionOptions.VerifyServerCertificate = (c, cert) => true; // ❌

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
