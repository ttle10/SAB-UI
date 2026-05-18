namespace SystemeAideBasculement.Services.Security
{
    public interface IAdLdapService
    {
        AuthResult Authenticate(string username, string password);
    }

}
