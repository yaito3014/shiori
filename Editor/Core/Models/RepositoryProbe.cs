namespace Shiori
{
    public enum RepositoryState
    {
        /// <summary>No repository contains the working directory.</summary>
        NotARepository,

        /// <summary>The working directory is the repository root.</summary>
        Ready,

        /// <summary>The working directory is inside a repository whose root is elsewhere (unsupported in M1).</summary>
        RootMismatch,
    }

    public sealed class RepositoryProbe
    {
        public RepositoryState State { get; }

        /// <summary>The repository root git reported, or null when not a repository.</summary>
        public string TopLevel { get; }

        public RepositoryProbe(RepositoryState state, string topLevel)
        {
            State = state;
            TopLevel = topLevel;
        }
    }
}
