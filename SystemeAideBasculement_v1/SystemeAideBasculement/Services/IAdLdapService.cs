namespace SystemeAideBasculement.Services
{
    public interface IAdLdapService
    {
        (bool Ok, bool IsCcsab, string? DisplayName) AuthenticateAndCheckCcsab(
                                                    string username, string password);
    }
}
