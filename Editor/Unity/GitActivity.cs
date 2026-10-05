using System;
using UnityEditor;

namespace Shiori.Editor
{
    /// <summary>
    /// Tracks whether a Shiori git operation is in flight so the UI can lock itself (F6),
    /// and surfaces each operation in the editor's Progress window.
    /// </summary>
    internal static class GitActivity
    {
        private static int _depth;

        public static bool IsBusy => _depth > 0;

        public static event Action Changed;

        /// <summary>Marks an operation as running until the returned handle is disposed. Always dispose on the main thread.</summary>
        public static IDisposable Begin(string title)
        {
            return new Scope(title);
        }

        private sealed class Scope : IDisposable
        {
            private readonly int _progressId;
            private bool _disposed;

            public Scope(string title)
            {
                _depth++;
                _progressId = Progress.Start(title, null, Progress.Options.Indefinite);
                Changed?.Invoke();
            }

            public void Dispose()
            {
                if (_disposed) return;
                _disposed = true;
                _depth--;
                if (Progress.Exists(_progressId)) Progress.Remove(_progressId);
                Changed?.Invoke();
            }
        }
    }
}
