using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Diagnostics;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

namespace Shiori
{
    /// <summary>Runs git through <see cref="Process"/>. Never blocks the calling thread while git runs.</summary>
    internal sealed class ProcessGitRunner : IGitRunner
    {
        private static readonly Encoding Utf8NoBom = new UTF8Encoding(false);

        public async Task<GitResult> RunAsync(string gitExecutable, IReadOnlyList<string> args, string workingDirectory, CancellationToken cancellationToken)
        {
            if (string.IsNullOrEmpty(gitExecutable)) throw new ArgumentException("git executable path is empty", nameof(gitExecutable));
            if (args == null) throw new ArgumentNullException(nameof(args));

            var commandText = CommandLine.Join(args);
            var startInfo = new ProcessStartInfo
            {
                FileName = gitExecutable,
                Arguments = commandText,
                WorkingDirectory = workingDirectory ?? string.Empty,
                UseShellExecute = false,
                CreateNoWindow = true,
                RedirectStandardInput = true,
                RedirectStandardOutput = true,
                RedirectStandardError = true,
                StandardOutputEncoding = Utf8NoBom,
                StandardErrorEncoding = Utf8NoBom,
            };
            // Never let git prompt on a console we cannot see; credentials go through the credential manager.
            startInfo.EnvironmentVariables["GIT_TERMINAL_PROMPT"] = "0";
            // Keep messages in English so the UI layer can match them when translating.
            startInfo.EnvironmentVariables["LC_ALL"] = "C";

            cancellationToken.ThrowIfCancellationRequested();

            var process = new Process { StartInfo = startInfo };
            try
            {
                try
                {
                    process.Start();
                }
                catch (Win32Exception ex)
                {
                    throw new GitException(commandText, -1, ex.Message, ex);
                }
                catch (InvalidOperationException ex)
                {
                    throw new GitException(commandText, -1, ex.Message, ex);
                }

                process.StandardInput.Close();

                var stdoutTask = Task.Run(() => process.StandardOutput.ReadToEnd());
                var stderrTask = Task.Run(() => process.StandardError.ReadToEnd());
                var exitTask = Task.Run(() => process.WaitForExit());

                using (cancellationToken.Register(() => TryKill(process)))
                {
                    await Task.WhenAll(stdoutTask, stderrTask, exitTask).ConfigureAwait(false);
                }

                cancellationToken.ThrowIfCancellationRequested();
                return new GitResult(process.ExitCode, stdoutTask.Result, stderrTask.Result);
            }
            finally
            {
                process.Dispose();
            }
        }

        private static void TryKill(Process process)
        {
            try
            {
                if (!process.HasExited) process.Kill();
            }
            catch (InvalidOperationException)
            {
                // already exited
            }
            catch (Win32Exception)
            {
                // could not be killed; nothing more to do
            }
        }
    }
}
