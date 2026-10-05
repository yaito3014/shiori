namespace Shiori
{
    /// <summary>Raw outcome of one git process invocation.</summary>
    public sealed class GitResult
    {
        public int ExitCode { get; }
        public string Stdout { get; }
        public string Stderr { get; }

        public bool Succeeded => ExitCode == 0;

        public GitResult(int exitCode, string stdout, string stderr)
        {
            ExitCode = exitCode;
            Stdout = stdout ?? string.Empty;
            Stderr = stderr ?? string.Empty;
        }
    }
}
