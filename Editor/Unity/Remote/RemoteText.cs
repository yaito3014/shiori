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
            if (status == null || !status.HasRemote) return L10n.Tr("send.none");
            if (status.NeverSent) return L10n.Tr("send.never", status.Unsent);
            return status.Unsent > 0 ? L10n.Tr("send.unsent", status.Unsent) : L10n.Tr("send.uptodate");
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
