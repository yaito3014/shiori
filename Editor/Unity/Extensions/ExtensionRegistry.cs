using System;
using System.Collections.Generic;
using UnityEditor;
using UnityEngine;

namespace Shiori.Editor
{
    /// <summary>
    /// Finds every installed <see cref="ShioriExtension"/> once per domain. A broken extension is
    /// logged and skipped so the core window still opens. Test assemblies are ignored so fakes
    /// used by tests never show up in a real wizard.
    /// </summary>
    internal static class ExtensionRegistry
    {
        private static IReadOnlyList<ShioriExtension> _all;

        public static IReadOnlyList<ShioriExtension> All => _all ?? (_all = Discover());

        /// <summary>Replaces the discovered list (tests); null goes back to discovery.</summary>
        internal static void Override(IReadOnlyList<ShioriExtension> extensions)
        {
            _all = extensions;
        }

        internal static IReadOnlyList<ShioriExtension> Discover()
        {
            var result = new List<ShioriExtension>();
            foreach (var type in TypeCache.GetTypesDerivedFrom<ShioriExtension>())
            {
                if (type.IsAbstract || type.ContainsGenericParameters) continue;
                if (IsTestAssembly(type.Assembly.GetName().Name)) continue;
                try
                {
                    var extension = (ShioriExtension)Activator.CreateInstance(type);
                    if (string.IsNullOrEmpty(extension.PackageId)) throw new InvalidOperationException("PackageId is empty");
                    result.Add(extension);
                }
                catch (Exception ex)
                {
                    // LogException would print only the inner exception, hiding which extension failed.
                    Debug.LogError("Shiori: extension " + type.FullName + " could not be loaded.\n" + ex);
                }
            }
            result.Sort((a, b) => string.CompareOrdinal(a.PackageId, b.PackageId));
            return result;
        }

        internal static bool IsTestAssembly(string assemblyName)
        {
            return assemblyName != null && assemblyName.EndsWith(".Tests", StringComparison.Ordinal);
        }
    }
}
