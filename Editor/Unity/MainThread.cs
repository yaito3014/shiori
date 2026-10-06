using System;
using System.Threading;
using UnityEditor;

namespace Shiori.Editor
{
    /// <summary>Returns work to the editor's main thread. Captured once per domain load.</summary>
    [InitializeOnLoad]
    internal static class MainThread
    {
        private static readonly SynchronizationContext Context;
        private static readonly int ThreadId;

        static MainThread()
        {
            Context = SynchronizationContext.Current;
            ThreadId = Thread.CurrentThread.ManagedThreadId;
        }

        public static bool IsMainThread => Thread.CurrentThread.ManagedThreadId == ThreadId;

        /// <summary>Runs <paramref name="action"/> on the main thread: immediately when already there, otherwise posted.</summary>
        public static void Post(Action action)
        {
            if (action == null) return;
            if (IsMainThread || Context == null)
            {
                action();
                return;
            }
            Context.Post(_ => action(), null);
        }
    }
}
