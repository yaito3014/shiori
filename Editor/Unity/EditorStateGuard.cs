using System;
using UnityEditor;

namespace Shiori.Editor
{
    /// <summary>Why git operations are currently blocked. <see cref="None"/> means they are allowed.</summary>
    internal enum LockReason
    {
        None,
        Compiling,
        Updating,
        PlayMode,
        GitBusy,
    }

    /// <summary>The editor facts the lock decision depends on. Kept as a plain struct so the rule is testable.</summary>
    internal readonly struct EditorStateSnapshot
    {
        public readonly bool IsCompiling;
        public readonly bool IsUpdating;
        public readonly bool IsPlayingOrWillChangePlaymode;
        public readonly bool IsGitBusy;

        public EditorStateSnapshot(bool isCompiling, bool isUpdating, bool isPlayingOrWillChangePlaymode, bool isGitBusy)
        {
            IsCompiling = isCompiling;
            IsUpdating = isUpdating;
            IsPlayingOrWillChangePlaymode = isPlayingOrWillChangePlaymode;
            IsGitBusy = isGitBusy;
        }

        public static EditorStateSnapshot Capture()
        {
            return new EditorStateSnapshot(
                EditorApplication.isCompiling,
                EditorApplication.isUpdating,
                EditorApplication.isPlayingOrWillChangePlaymode,
                GitActivity.IsBusy);
        }
    }

    /// <summary>
    /// Decides whether git may run (F6) and notifies listeners when that decision changes.
    /// The editor exposes no single event for every condition, so the live guard polls once per editor update.
    /// </summary>
    internal sealed class EditorStateGuard : IDisposable
    {
        private LockReason _current;
        private bool _disposed;

        public event Action<LockReason> Changed;

        public LockReason Current => _current;

        public bool IsLocked => _current != LockReason.None;

        public EditorStateGuard()
        {
            _current = Evaluate(EditorStateSnapshot.Capture());
            EditorApplication.update += Poll;
        }

        /// <summary>Pure rule: the editor conditions win over git activity so the message explains the real blocker.</summary>
        public static LockReason Evaluate(EditorStateSnapshot state)
        {
            if (state.IsCompiling) return LockReason.Compiling;
            if (state.IsUpdating) return LockReason.Updating;
            if (state.IsPlayingOrWillChangePlaymode) return LockReason.PlayMode;
            if (state.IsGitBusy) return LockReason.GitBusy;
            return LockReason.None;
        }

        /// <summary>Re-evaluates immediately (for example right after a git operation starts or ends).</summary>
        public void Refresh()
        {
            Poll();
        }

        private void Poll()
        {
            if (_disposed) return;
            var next = Evaluate(EditorStateSnapshot.Capture());
            if (next == _current) return;
            _current = next;
            Changed?.Invoke(next);
        }

        public void Dispose()
        {
            if (_disposed) return;
            _disposed = true;
            EditorApplication.update -= Poll;
            Changed = null;
        }
    }
}
