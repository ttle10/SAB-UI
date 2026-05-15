namespace SystemeAideBasculement.Services.Security
{
    public class OrgranisationUnit
    {
        private const string UNIT_NAME = "CCSAB";

        public string Name {
            get
            { 
                return UNIT_NAME;
            }
            
            }
        public bool IsValidUnit(string unitName)
        {
            return unitName.Equals(UNIT_NAME, StringComparison.OrdinalIgnoreCase);
        }
    }
}
