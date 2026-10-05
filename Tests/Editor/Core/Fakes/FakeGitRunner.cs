using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;

namespace Shiori.Tests
{
    /// <summary>Returns canned results keyed by the space-joined argument list and records every call.</summary>
    internal sealed class FakeGitRunner : IGitRunner
    {
        public sealed class Call
        {
            public string Executable;
            public IReadOnlyList<string> Args;
            public string WorkingDirectory;
            public string Joined => string.Join(" ", Args);
        }

        private readonly Dictionary<string, Func<GitResult>> _responses = new Dictionary<string, Func<GitResult>>(StringComparer.Ordinal);
        public List<Call> Calls { get; } = new List<Call>();

        /// <summary>Result for argument lists that have no registered response.</summary>
        public GitResult Default { get; set; } = new GitResult(0, string.Empty, string.Empty);

        public FakeGitRunner On(string joinedArgs, string stdout, int exitCode = 0, string stderr = "")
        {
            _responses[joinedArgs] = () => new GitResult(exitCode, stdout, stderr);
            return this;
        }

        public FakeGitRunner On(string joinedArgs, Func<GitResult> factory)
        {
            _responses[joinedArgs] = factory;
            return this;
        }

        public Task<GitResult> RunAsync(string gitExecutable, IReadOnlyList<string> args, string workingDirectory, CancellationToken cancellationToken)
        {
            var call = new Call { Executable = gitExecutable, Args = new List<string>(args), WorkingDirectory = workingDirectory };
            Calls.Add(call);
            return Task.FromResult(_responses.TryGetValue(call.Joined, out var factory) ? factory() : Default);
        }
    }
}
