using System;

namespace Shiori
{
    public enum DiffLineKind
    {
        /// <summary>"diff --git", "index", "---", "+++", "new file mode" and similar.</summary>
        Header,

        /// <summary>"@@ -a,b +c,d @@" hunk markers.</summary>
        Hunk,

        Context,
        Added,
        Removed,

        /// <summary>"\ No newline at end of file".</summary>
        Meta,
    }

    public sealed class DiffLine
    {
        public DiffLineKind Kind { get; }

        /// <summary>The line including its leading '+', '-' or ' ' marker.</summary>
        public string Text { get; }

        public DiffLine(DiffLineKind kind, string text)
        {
            Kind = kind;
            Text = text ?? throw new ArgumentNullException(nameof(text));
        }
    }
}
