namespace SystemeAideBasculement.Services.Security
{
    public interface IOrgranisationUnit
    {
        string GroupName { get; }
        
        bool IsAuthorized();
    }

    public class DefaultUnit : IOrgranisationUnit
    {
        private const string GROUP_NAME = "DEFAULT";
        private bool  _isAuthorized = false;

        public string GroupName
        {
            get
            { 
                return GROUP_NAME;
            }
            
            }

        public bool IsAuthorized()
        {
            return _isAuthorized;
        }
    }

    public class SABUnit : IOrgranisationUnit
    {
        private const string GROUP_NAME = "CCSAB";

        private bool _isAuthorized = true;

        public string GroupName
        {
            get
            {
                return GROUP_NAME;
            }

        }
        public bool IsAuthorized()
        {
            return _isAuthorized;
        }

        public bool IsValidGroup(string groupName)
        {
            return groupName.Equals(GROUP_NAME, StringComparison.OrdinalIgnoreCase);
        }
    }

}
