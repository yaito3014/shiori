using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;

namespace Shiori
{
    /// <summary>
    /// Executes a git executable with the given arguments. The only production
    /// implementation is <c>ProcessGitRunner</c>; tests inject fakes.
    /// </summary>
    public interface IGitRunner
    {
        /// <summary>
        /// Runs <paramref name="gitExecutable"/> with <paramref name="args"/> (not shell-quoted;
        /// each element is one argument) in <paramref name="workingDirectory"/>.
        /// Never throws on a non-zero exit code; the caller inspects <see cref="GitResult"/>.
        /// Throws <see cref="GitException"/> only when the process cannot be started.
        /// </summary>
        Task<GitResult> RunAsync(string gitExecutable, IReadOnlyList<string> args, string workingDirectory, CancellationToken cancellationToken);
    }
}
