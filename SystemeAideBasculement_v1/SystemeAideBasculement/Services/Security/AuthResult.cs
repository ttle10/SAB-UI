namespace SystemeAideBasculement.Services.Security
{
    public class AuthResult
    {
        public bool IsAuthenticated { get; init; }
        public IOrgranisationUnit Unit { get; set; } = new DefaultUnit();
        public string? UserDisplayName { get; init; }
    }
}
