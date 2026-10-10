using System;
using System.Threading;
using System.Threading.Tasks;

namespace Shiori.Editor
{
    /// <summary>
    /// The quiet background check for new saves at the 送信先, shared by both modes so they never
    /// fetch more often than <see cref="Interval"/> between them. It never asks for sign-in and never
    /// reports errors: if the 送信先 cannot be reached quietly the caller keeps what it had.
    /// </summary>
    internal static class RemoteWatch
    {
        public static readonly TimeSpan Interval = TimeSpan.FromMinutes(5);

        private static DateTime _last = DateTime.MinValue;
        private static bool _running;

        /// <summary>Records that a fetch just happened (for example a 受信), so the next check can wait.</summary>
        public static void MarkFetched()
        {
            _last = DateTime.UtcNow;
        }

        /// <summary>
        /// Fetches quietly when the last fetch is older than <see cref="Interval"/> and returns the new
        /// comparison, or null when no check ran or it failed.
        /// </summary>
        public static async Task<RemoteComparison> CheckIfDueAsync(IGitRepository repository)
        {
            if (_running || repository == null) return null;
            if (DateTime.UtcNow - _last < Interval) return null;
            _last = DateTime.UtcNow;
            _running = true;
            try
            {
                return await ReceiveRunner.CheckAsync(repository, CancellationToken.None);
            }
            catch (Exception)
            {
                // Offline, signed out or slow: say nothing; a 受信 / pull itself reports the real problem.
                return null;
            }
            finally
            {
                _running = false;
            }
        }
    }
}
