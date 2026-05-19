namespace SystemeAideBasculement.Services.Security
{
    public interface IAdLdapService
    {
        (bool Ok, bool IsCcsab, string? DisplayName) AuthenticateAndCheckCcsab(
                                                    string username, string password);
    }
}
