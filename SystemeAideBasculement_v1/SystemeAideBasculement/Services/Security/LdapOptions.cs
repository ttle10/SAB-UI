namespace SystemeAideBasculement.Services.Security
{
    public class LdapOptions
    {
        public List<string> Hosts { get; set; } = new List<string>();
        public int Port { get; set; } = 636;
        public string BaseDn { get; set; } = "";
        public string DomainNetbios { get; set; } = "";
        public string CcsabGroupDn { get; set; } = "";
    }
}
