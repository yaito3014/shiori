using System;
using System.Collections.Generic;
using System.IO;
using System.Text;

namespace Shiori
{
    /// <summary>A settings file exists but cannot be read. The UI offers to reset it.</summary>
    public sealed class SettingsFormatException : Exception
    {
        public string FilePath { get; }

        public SettingsFormatException(string filePath, string message, Exception inner = null)
            : base($"{filePath}: {message}", inner)
        {
            FilePath = filePath;
        }
    }

    /// <summary>
    /// Reads and writes Shiori's two JSON settings files. Missing files yield defaults;
    /// unknown keys are ignored so newer files still load in older versions.
    /// </summary>
    internal sealed class SettingsStore
    {
        private static readonly Encoding Utf8NoBom = new UTF8Encoding(false);

        public string ProjectRoot { get; }
        public string ProjectSettingsPath => Path.Combine(ProjectRoot, "ProjectSettings", "Shiori.json");
        public string UserSettingsPath => Path.Combine(ProjectRoot, "UserSettings", "Shiori.json");

        public SettingsStore(string projectRoot)
        {
            if (string.IsNullOrEmpty(projectRoot)) throw new ArgumentException("project root is required", nameof(projectRoot));
            ProjectRoot = projectRoot;
        }

        public ShioriProjectSettings LoadProject()
        {
            var settings = new ShioriProjectSettings();
            var json = ReadObject(ProjectSettingsPath);
            if (json == null) return settings;
            settings.SchemaVersion = GetInt(json, "schemaVersion", settings.SchemaVersion);
            settings.SetupCompleted = GetBool(json, "setupCompleted", settings.SetupCompleted);
            if (json.TryGetValue("extensions", out var extensions) && extensions is Dictionary<string, object> sections)
            {
                foreach (var pair in sections)
                {
                    // A section that is not an object is dropped; nothing in the core depends on it.
                    if (pair.Value is Dictionary<string, object> section) settings.Extensions[pair.Key] = section;
                }
            }
            return settings;
        }

        public void SaveProject(ShioriProjectSettings settings)
        {
            if (settings == null) throw new ArgumentNullException(nameof(settings));
            var json = new Dictionary<string, object>
            {
                ["schemaVersion"] = settings.SchemaVersion,
                ["setupCompleted"] = settings.SetupCompleted,
            };
            // Only written when some extension stored something, so projects without add-ons keep the M1 file shape.
            var sections = new Dictionary<string, object>(StringComparer.Ordinal);
            foreach (var pair in settings.Extensions)
            {
                if (pair.Value != null && pair.Value.Count > 0) sections[pair.Key] = pair.Value;
            }
            if (sections.Count > 0) json["extensions"] = sections;
            WriteObject(ProjectSettingsPath, json);
        }

        public ShioriUserSettings LoadUser()
        {
            var settings = new ShioriUserSettings();
            var json = ReadObject(UserSettingsPath);
            if (json == null) return settings;
            settings.SchemaVersion = GetInt(json, "schemaVersion", settings.SchemaVersion);
            settings.GitPath = GetString(json, "gitPath", settings.GitPath);
            settings.Mode = ParseMode(GetString(json, "mode", null), settings.Mode);
            settings.Modes = ParseModes(GetString(json, "modes", null), settings.Modes);
            settings.LastTab = GetString(json, "lastTab", settings.LastTab);
            return settings;
        }

        public void SaveUser(ShioriUserSettings settings)
        {
            if (settings == null) throw new ArgumentNullException(nameof(settings));
            var json = new Dictionary<string, object>
            {
                ["schemaVersion"] = settings.SchemaVersion,
                ["gitPath"] = settings.GitPath ?? string.Empty,
                ["mode"] = settings.Mode == UiMode.Detail ? "detail" : "simple",
                ["modes"] = ModesToString(settings.Modes),
                ["lastTab"] = settings.LastTab ?? string.Empty,
            };
            WriteObject(UserSettingsPath, json);
        }

        internal static UiMode ParseMode(string text, UiMode fallback)
        {
            if (string.Equals(text, "detail", StringComparison.OrdinalIgnoreCase)) return UiMode.Detail;
            if (string.Equals(text, "simple", StringComparison.OrdinalIgnoreCase)) return UiMode.Simple;
            return fallback;
        }

        internal static ModeAvailability ParseModes(string text, ModeAvailability fallback)
        {
            if (string.Equals(text, "both", StringComparison.OrdinalIgnoreCase)) return ModeAvailability.Both;
            if (string.Equals(text, "simple", StringComparison.OrdinalIgnoreCase)) return ModeAvailability.SimpleOnly;
            if (string.Equals(text, "detail", StringComparison.OrdinalIgnoreCase)) return ModeAvailability.DetailOnly;
            return fallback;
        }

        internal static string ModesToString(ModeAvailability modes)
        {
            switch (modes)
            {
                case ModeAvailability.SimpleOnly: return "simple";
                case ModeAvailability.DetailOnly: return "detail";
                default: return "both";
            }
        }

        private static Dictionary<string, object> ReadObject(string path)
        {
            if (!File.Exists(path)) return null;
            string text;
            try
            {
                text = File.ReadAllText(path, Utf8NoBom);
            }
            catch (IOException ex)
            {
                throw new SettingsFormatException(path, "cannot read file", ex);
            }

            object parsed;
            try
            {
                parsed = MiniJson.Parse(text);
            }
            catch (FormatException ex)
            {
                throw new SettingsFormatException(path, ex.Message, ex);
            }

            return parsed as Dictionary<string, object> ?? throw new SettingsFormatException(path, "top-level value is not an object");
        }

        private static void WriteObject(string path, Dictionary<string, object> json)
        {
            var directory = Path.GetDirectoryName(path);
            if (!string.IsNullOrEmpty(directory)) Directory.CreateDirectory(directory);
            var text = MiniJson.Serialize(json);
            // Write to a sibling temp file first so a crash mid-write cannot leave a truncated settings file.
            var temp = path + ".tmp";
            File.WriteAllText(temp, text, Utf8NoBom);
            if (File.Exists(path)) File.Delete(path);
            File.Move(temp, path);
        }

        private static int GetInt(Dictionary<string, object> json, string key, int fallback)
        {
            if (!json.TryGetValue(key, out var value)) return fallback;
            switch (value)
            {
                case long l: return l >= int.MinValue && l <= int.MaxValue ? (int)l : fallback;
                case double d: return d >= int.MinValue && d <= int.MaxValue ? (int)d : fallback;
                default: return fallback;
            }
        }

        private static bool GetBool(Dictionary<string, object> json, string key, bool fallback)
        {
            return json.TryGetValue(key, out var value) && value is bool b ? b : fallback;
        }

        private static string GetString(Dictionary<string, object> json, string key, string fallback)
        {
            return json.TryGetValue(key, out var value) && value is string s ? s : fallback;
        }
    }
}
