using System;
using System.Collections.Generic;

namespace Shiori
{
    /// <summary>Contents of <c>ProjectSettings/Shiori.json</c>, which is committed with the project.</summary>
    public sealed class ShioriProjectSettings
    {
        public const int CurrentSchemaVersion = 1;

        public int SchemaVersion { get; set; } = CurrentSchemaVersion;

        /// <summary>True once the setup wizard has run to completion for this project.</summary>
        public bool SetupCompleted { get; set; }

        /// <summary>
        /// One section per extension, keyed by package id (the <c>extensions</c> object in the file).
        /// Sections of packages that are not installed are kept as they are.
        /// </summary>
        public Dictionary<string, Dictionary<string, object>> Extensions { get; } = new Dictionary<string, Dictionary<string, object>>(StringComparer.Ordinal);

        /// <summary>The section for <paramref name="packageId"/>, created empty on first use.</summary>
        public IDictionary<string, object> GetExtensionSettings(string packageId)
        {
            if (string.IsNullOrEmpty(packageId)) throw new ArgumentException("package id is required", nameof(packageId));
            if (!Extensions.TryGetValue(packageId, out var section))
            {
                section = new Dictionary<string, object>(StringComparer.Ordinal);
                Extensions[packageId] = section;
            }
            return section;
        }
    }
}
