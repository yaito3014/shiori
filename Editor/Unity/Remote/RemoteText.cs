using System;

namespace Shiori.Editor
{
    /// <summary>User-facing text for 送信 results and remote failures.</summary>
    internal static class RemoteText
    {
        /// <summary>A plain explanation of a remote failure; git's own lines follow when they help (size, unknown).</summary>
        public static string Describe(RemoteOperationException ex)
        {
            var text = L10n.Tr("remote.error." + ex.Kind);
            if (ex.Kind == RemoteErrorKind.TooLarge || ex.Kind == RemoteErrorKind.Unknown)
            {
                var detail = ex.Stderr.Trim();
                if (detail.Length > 0) text += "\n" + Shorten(detail, 600);
            }
            return text;
        }

        public static string Describe(SendResult result)
        {
            string text;
            switch (result.Outcome)
            {
                case SendOutcome.Sent: text = L10n.Tr("send.done", result.Count); break;
                case SendOutcome.NothingToSend: text = L10n.Tr("send.nothing"); break;
                case SendOutcome.NothingSaved: text = L10n.Tr("send.nothingsaved"); break;
                default: text = L10n.Tr("send.none"); break;
            }
            if (result.HadUnsavedChanges && result.Outcome != SendOutcome.NoRemote) text += "\n" + L10n.Tr("send.unsaved");
            return text;
        }

        /// <summary>The one-line state shown next to the 送信 button.</summary>
        public static string Status(SendStatus status)
        {
            return Status(status, null);
        }

        /// <summary>
        /// The one-line state next to 送信 / 受信: unsent saves, plus what the last check found at the
        /// 送信先 (waiting saves, or that the two sides saved separately).
        /// </summary>
        public static string Status(SendStatus status, RemoteComparison comparison)
        {
            if (status == null || !status.HasRemote) return L10n.Tr("send.none");
            if (comparison != null && comparison.Known && comparison.Diverged) return L10n.Tr("receive.split");
            var parts = new System.Collections.Generic.List<string>();
            if (status.NeverSent) parts.Add(L10n.Tr("send.never", status.Unsent));
            else if (status.Unsent > 0) parts.Add(L10n.Tr("send.unsent", status.Unsent));
            if (comparison != null && comparison.Known && comparison.Behind > 0) parts.Add(L10n.Tr("receive.waiting", comparison.Behind));
            return parts.Count == 0 ? L10n.Tr("send.uptodate") : string.Join(" ・ ", parts);
        }

        public static string Describe(ReceiveResult result)
        {
            switch (result.Outcome)
            {
                case ReceiveOutcome.Received: return L10n.Tr("receive.done", result.Count);
                case ReceiveOutcome.NothingToReceive: return L10n.Tr("receive.nothing");
                case ReceiveOutcome.UnsavedChanges: return L10n.Tr("receive.unsaved");
                case ReceiveOutcome.Diverged: return L10n.Tr("receive.diverged", result.Count);
                case ReceiveOutcome.Unrelated: return L10n.Tr("receive.unrelated");
                case ReceiveOutcome.NothingSaved: return L10n.Tr("send.nothingsaved");
                default: return L10n.Tr("send.none");
            }
        }

        public static string Describe(RemoteCheckState state)
        {
            switch (state)
            {
                case RemoteCheckState.Empty: return L10n.Tr("remote.empty");
                case RemoteCheckState.SameHistory: return L10n.Tr("remote.same");
                default: return L10n.Tr("remote.other");
            }
        }

        private static string Shorten(string text, int max)
        {
            return text.Length <= max ? text : text.Substring(0, max) + "…";
        }

        /// <summary>Any exception from a remote action, as text for the error label.</summary>
        public static string DescribeAny(Exception ex)
        {
            return ex is RemoteOperationException remote ? Describe(remote) : SetupWizardView.Describe(ex);
        }
    }
}
