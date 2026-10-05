namespace Shiori
{
    /// <summary>Effective <c>user.name</c> / <c>user.email</c> as git resolves them for a repository.</summary>
    public sealed class GitIdentity
    {
        public string Name { get; }
        public string Email { get; }

        public bool IsComplete => !string.IsNullOrWhiteSpace(Name) && !string.IsNullOrWhiteSpace(Email);

        public GitIdentity(string name, string email)
        {
            Name = name ?? string.Empty;
            Email = email ?? string.Empty;
        }
    }
}
