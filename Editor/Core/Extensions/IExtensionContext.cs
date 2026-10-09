using System.Collections.Generic;

namespace Shiori
{
    /// <summary>
    /// What the core hands to an extension: where the project is, the repository, and the two
    /// things an extension may write — its own managed blocks in <c>.gitignore</c> /
    /// <c>.gitattributes</c>, and its own section of <c>ProjectSettings/Shiori.json</c>.
    /// Extensions must not write anything else inside the user's project.
    /// </summary>
    public interface IExtensionContext
    {
        /// <summary>The Unity project root (the parent of <c>Assets/</c>).</summary>
        string ProjectRoot { get; }

        /// <summary>UI language as an ISO 639-1 code: <c>ja</c> or <c>en</c>.</summary>
        string LanguageCode { get; }

        /// <summary>The project's repository, or null while git has not been located yet.</summary>
        IGitRepository Repository { get; }

        /// <summary>
        /// Lines inside the block <paramref name="blockId"/> of <paramref name="fileName"/>
        /// (<c>.gitignore</c> or <c>.gitattributes</c>), or null when the file or the block is absent.
        /// </summary>
        IReadOnlyList<string> ReadManagedBlock(string fileName, string blockId);

        /// <summary>
        /// Writes or updates the block <paramref name="blockId"/> in <paramref name="fileName"/>
        /// (<c>.gitignore</c> or <c>.gitattributes</c>), keeping user lines and other blocks.
        /// Returns true when the file changed.
        /// </summary>
        bool UpsertManagedBlock(string fileName, string blockId, IReadOnlyList<string> lines);

        /// <summary>
        /// The extension's section of <c>ProjectSettings/Shiori.json</c>, keyed by its package id.
        /// Values are JSON-shaped: string, bool, long, double, List&lt;object&gt; or Dictionary&lt;string, object&gt;.
        /// Call <see cref="SaveSettings"/> after changing it.
        /// </summary>
        IDictionary<string, object> Settings { get; }

        void SaveSettings();
    }
}
