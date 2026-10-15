using System;
using System.Threading;
using System.Threading.Tasks;
using UnityEditor;

namespace Shiori.Editor
{
    /// <summary>
    /// Stops the operations that take files out of history (受信, 戻す, switching a variation, merging, taking out a
    /// set-aside change) while the project uses Git LFS and this PC cannot run it: they would write LFS pointer text
    /// in place of textures and models. 保存 and 送信 are not affected.
    /// </summary>
    internal static class LargeFileGuard
    {
        public static async Task<bool> IsBlockedAsync(ShioriSession session, CancellationToken cancellationToken)
        {
            var repository = session.Repository;
            if (repository == null) return false;
            try
            {
                return (await LargeFileSetup.EvaluateAsync(repository, false, cancellationToken)).Blocked;
            }
            catch (GitException)
            {
                // The setup step shows the failure; an unreadable state must not lock the window.
                return false;
            }
        }

        /// <summary>True when the operation may go ahead; otherwise explains why not and returns false.</summary>
        public static async Task<bool> AllowAsync(ShioriSession session, CancellationToken cancellationToken)
        {
            if (!await IsBlockedAsync(session, cancellationToken)) return true;
            EditorUtility.DisplayDialog(L10n.Tr("lfs.blocked.title"), L10n.Tr("lfs.blocked.body"), L10n.Tr("lfs.blocked.ok"));
            return false;
        }
    }
}
