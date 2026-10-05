namespace Shiori
{
    public enum UiMode
    {
        Simple,
        Detail,
    }

    /// <summary>Contents of <c>UserSettings/Shiori.json</c>, which stays local to one machine.</summary>
    public sealed class ShioriUserSettings
    {
        public const int CurrentSchemaVersion = 1;

        public int SchemaVersion { get; set; } = CurrentSchemaVersion;

        /// <summary>Explicit git executable path chosen by the user; empty means "search automatically".</summary>
        public string GitPath { get; set; } = string.Empty;

        public UiMode Mode { get; set; } = UiMode.Simple;

        /// <summary>Identifier of the tab that was open last; empty when none.</summary>
        public string LastTab { get; set; } = string.Empty;
    }
}
