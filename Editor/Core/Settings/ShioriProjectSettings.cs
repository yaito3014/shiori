namespace Shiori
{
    /// <summary>Contents of <c>ProjectSettings/Shiori.json</c>, which is committed with the project.</summary>
    public sealed class ShioriProjectSettings
    {
        public const int CurrentSchemaVersion = 1;

        public int SchemaVersion { get; set; } = CurrentSchemaVersion;

        /// <summary>True once the setup wizard has run to completion for this project.</summary>
        public bool SetupCompleted { get; set; }
    }
}
