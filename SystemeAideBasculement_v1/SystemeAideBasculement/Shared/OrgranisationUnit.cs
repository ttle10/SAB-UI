namespace SystemeAideBasculement.Shared
{
    public interface IOrgranisationUnit
    {
        string Role { get; }
        bool IsAuthorized();
    }

    public class DefaultUnit : IOrgranisationUnit
    {
        private const string ROLE = "DEFAULT_ROLE";
        private bool  _isAuthorized = false;

        public string Role
        {
            get
            {
                return ROLE;
            }
        }

        public bool IsAuthorized()
        {
            return _isAuthorized;
        }
    }

    public class SABUnit : IOrgranisationUnit
    {
        private const string ROLE_CC = "CCSAB";

        private bool _isAuthorized = true;

        public string Role
        {
            get
            {
                return ROLE_CC;
            }
        }

        public bool IsAuthorized()
        {
            return _isAuthorized;
        }

        public bool IsValidRole(string? role)
        {
            return role != null && role.Equals(ROLE_CC, StringComparison.OrdinalIgnoreCase);
        }
    }

}
