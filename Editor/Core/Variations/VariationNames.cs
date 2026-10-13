using System;
using System.Text;

namespace Shiori
{
    /// <summary>
    /// How バリエーション names map to branches: a variation 「衣装A」 is the branch <c>shiori/衣装A</c>, and the
    /// branch the project started on (<c>main</c> or <c>master</c>) is 「メイン」. Other branches, made in detail
    /// mode or outside Shiori, are shown by their own name.
    /// </summary>
    public static class VariationNames
    {
        public const string Prefix = "shiori/";

        public static bool IsMain(string branch)
        {
            return branch == "main" || branch == "master";
        }

        /// <summary>The name to show for <paramref name="branch"/>: the part after <see cref="Prefix"/>, or the branch name itself.</summary>
        public static string Display(string branch)
        {
            if (branch == null) return string.Empty;
            return branch.StartsWith(Prefix, StringComparison.Ordinal) && branch.Length > Prefix.Length
                ? branch.Substring(Prefix.Length)
                : branch;
        }

        /// <summary>
        /// The branch for a typed name, with what git forbids in a ref name (spaces, <c>~^:?*[\</c>, "..", a
        /// trailing ".lock" and so on) turned into '-'. Null when nothing usable is left. Git still has the last
        /// word (<see cref="IGitRepository.IsValidBranchNameAsync"/>).
        /// </summary>
        public static string ToBranch(string displayName)
        {
            if (displayName == null) return null;
            var sb = new StringBuilder();
            foreach (var c in displayName.Trim())
            {
                // '/' would make a folder of variations; keep the names flat.
                var bad = c < ' ' || c == '\x7f' || char.IsWhiteSpace(c) || "~^:?*[\\/".IndexOf(c) >= 0;
                sb.Append(bad ? '-' : c);
            }
            var name = sb.ToString();
            while (name.Contains("..")) name = name.Replace("..", ".");
            name = name.Replace("@{", "@-");
            while (name.Contains("--")) name = name.Replace("--", "-");
            if (name.EndsWith(".lock", StringComparison.OrdinalIgnoreCase)) name = name.Substring(0, name.Length - ".lock".Length);
            name = name.Trim('-', '.');
            if (name.Length == 0 || name == "@") return null;
            return Prefix + name;
        }
    }
}
