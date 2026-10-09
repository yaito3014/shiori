using System;
using System.Collections.Generic;

namespace Shiori
{
    /// <summary>
    /// Changes that 戻す set aside instead of throwing away (「保存せずに戻す」), i.e. a stash Shiori
    /// made itself. Stashes made by hand are not listed in simple mode.
    /// </summary>
    public sealed class SetAsideChange
    {
        /// <summary>Marker at the start of every Shiori stash subject.</summary>
        public const string Marker = "shiori:auto-before-restore";

        public StashEntry Stash { get; }

        /// <summary>Memo of the snapshot the user was going back to, or empty for stashes made before it was recorded.</summary>
        public string RestoreTarget { get; }

        public DateTimeOffset Time => Stash.Time;

        private SetAsideChange(StashEntry stash, string restoreTarget)
        {
            Stash = stash;
            RestoreTarget = restoreTarget ?? string.Empty;
        }

        /// <summary>The stash message for setting changes aside before going back to <paramref name="restoreTarget"/>.</summary>
        public static string MessageFor(string restoreTarget)
        {
            var target = (restoreTarget ?? string.Empty).Replace('\r', ' ').Replace('\n', ' ').Trim();
            return target.Length == 0 ? Marker : Marker + ": " + target;
        }

        /// <summary>Picks Shiori's stashes out of <c>git stash list</c>, newest first as git lists them.</summary>
        public static IReadOnlyList<SetAsideChange> FromStashList(IEnumerable<StashEntry> stashes)
        {
            var result = new List<SetAsideChange>();
            if (stashes == null) return result;
            foreach (var stash in stashes)
            {
                // git prefixes the subject with "On <branch>: ".
                var index = stash.Message.IndexOf(Marker, StringComparison.Ordinal);
                if (index < 0) continue;
                var rest = stash.Message.Substring(index + Marker.Length);
                var target = rest.StartsWith(": ", StringComparison.Ordinal) ? rest.Substring(2).Trim() : string.Empty;
                result.Add(new SetAsideChange(stash, target));
            }
            return result;
        }
    }
}
