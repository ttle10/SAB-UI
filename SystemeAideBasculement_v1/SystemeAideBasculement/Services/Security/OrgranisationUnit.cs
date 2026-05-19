namespace SystemeAideBasculement.Services.Security
{
    public interface IOrgranisationUnit
    {
        string Name { get; }
        
        bool IsAuthorized();
    }

    public class DefaultUnit : IOrgranisationUnit
    {
        private const string UNIT_NAME = "DEFAULT";
        private bool  _isAuthorized = false;

        public string Name {
            get
            { 
                return UNIT_NAME;
            }
            
            }

        public bool IsAuthorized()
        {
            return _isAuthorized;
        }
    }

    public class SABUnit : IOrgranisationUnit
    {
        private const string UNIT_NAME = "CCSAB";

        private bool _isAuthorized = true;

        public string Name
        {
            get
            {
                return UNIT_NAME;
            }

        }
        public bool IsAuthorized()
        {
            return _isAuthorized;
        }

        public bool IsValidUnit(string unitName)
        {
            return unitName.Equals(UNIT_NAME, StringComparison.OrdinalIgnoreCase);
        }
    }

}
