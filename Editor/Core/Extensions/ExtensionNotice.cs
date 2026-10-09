using System;
using System.Collections.Generic;

namespace Shiori
{
    /// <summary>
    /// A notice an extension shows at the top of simple mode. Notices are derived from the project's
    /// current state each time the view refreshes, so one disappears by itself once its cause is gone
    /// (for example after VCC resolved the packages). Text is localized by the extension.
    /// </summary>
    public sealed class ExtensionNotice
    {
        /// <summary>Stable identifier, used in tests and logs (for example <c>vrchat.packages-out-of-date</c>).</summary>
        public string Id { get; }

        /// <summary>Short heading, or empty.</summary>
        public string Title { get; }

        public string Message { get; }

        /// <summary>Secondary text such as a list of packages, or empty.</summary>
        public string Detail { get; }

        /// <summary>Buttons; the notice is evaluated again after one runs.</summary>
        public IReadOnlyList<SetupStepAction> Actions { get; }

        public ExtensionNotice(string id, string title, string message, string detail = null, IReadOnlyList<SetupStepAction> actions = null)
        {
            if (string.IsNullOrEmpty(id)) throw new ArgumentException("id is required", nameof(id));
            Id = id;
            Title = title ?? string.Empty;
            Message = message ?? string.Empty;
            Detail = detail ?? string.Empty;
            Actions = actions ?? Array.Empty<SetupStepAction>();
        }
    }
}
