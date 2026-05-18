namespace SystemeAideBasculement.Services.Security
{
    public class LdapOptions
    {
        public string Host { get; set; } = "";
        public int Port { get; set; } = 636;
        public string BaseDn { get; set; } = "";
        public string DomainNetbios { get; set; } = "";
        public string CcsabGroupDn { get; set; } = "";
    }
}
