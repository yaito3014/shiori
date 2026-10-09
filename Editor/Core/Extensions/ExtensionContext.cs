using System;
using System.Collections.Generic;
using System.IO;

namespace Shiori
{
    /// <summary>The core's <see cref="IExtensionContext"/>: one instance per extension per session.</summary>
    internal sealed class ExtensionContext : IExtensionContext
    {
        private static readonly string[] ManagedFiles = { ".gitignore", ".gitattributes" };

        private readonly Func<IGitRepository> _repository;
        private readonly Func<ShioriProjectSettings> _project;
        private readonly Action _saveProject;
        private readonly string _packageId;

        public string ProjectRoot { get; }
        public string LanguageCode { get; }
        public IGitRepository Repository => _repository();

        // Resolved on every access: the session re-reads its settings object after a settings page saved.
        public IDictionary<string, object> Settings => _project().GetExtensionSettings(_packageId);

        public ExtensionContext(string projectRoot, string languageCode, Func<IGitRepository> repository, Func<ShioriProjectSettings> project, Action saveProject, string packageId)
        {
            if (string.IsNullOrEmpty(projectRoot)) throw new ArgumentException("project root is required", nameof(projectRoot));
            if (string.IsNullOrEmpty(packageId)) throw new ArgumentException("package id is required", nameof(packageId));
            ProjectRoot = projectRoot;
            LanguageCode = languageCode ?? "en";
            _repository = repository ?? throw new ArgumentNullException(nameof(repository));
            _project = project ?? throw new ArgumentNullException(nameof(project));
            _saveProject = saveProject ?? throw new ArgumentNullException(nameof(saveProject));
            _packageId = packageId;
        }

        public IReadOnlyList<string> ReadManagedBlock(string fileName, string blockId)
        {
            var path = ManagedFilePath(fileName);
            if (!File.Exists(path)) return null;
            return ManagedBlockWriter.ReadBlock(File.ReadAllText(path), blockId);
        }

        public bool UpsertManagedBlock(string fileName, string blockId, IReadOnlyList<string> lines)
        {
            return ManagedBlockWriter.UpsertFile(ManagedFilePath(fileName), blockId, lines);
        }

        public void SaveSettings()
        {
            _saveProject();
        }

        /// <summary>Only the two files the safety rules allow; anything else (including paths) is refused.</summary>
        internal string ManagedFilePath(string fileName)
        {
            foreach (var allowed in ManagedFiles)
            {
                if (string.Equals(fileName, allowed, StringComparison.Ordinal)) return Path.Combine(ProjectRoot, allowed);
            }
            throw new ArgumentException("extensions may only write managed blocks to .gitignore or .gitattributes, not " + fileName, nameof(fileName));
        }
    }
}
