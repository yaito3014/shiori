namespace Shiori
{
    public enum UiMode
    {
        Simple,
        Detail,
    }

    /// <summary>Which modes the window offers. With a single mode the toggle is hidden.</summary>
    public enum ModeAvailability
    {
        Both,
        SimpleOnly,
        DetailOnly,
    }

    /// <summary>Contents of <c>UserSettings/Shiori.json</c>, which stays local to one machine.</summary>
    public sealed class ShioriUserSettings
    {
        public const int CurrentSchemaVersion = 1;

        public int SchemaVersion { get; set; } = CurrentSchemaVersion;

        /// <summary>Explicit git executable path chosen by the user; empty means "search automatically".</summary>
        public string GitPath { get; set; } = string.Empty;

        /// <summary>The mode last chosen with the toggle. Only meaningful when <see cref="Modes"/> is Both.</summary>
        public UiMode Mode { get; set; } = UiMode.Simple;

        public ModeAvailability Modes { get; set; } = ModeAvailability.Both;

        /// <summary>Identifier of the tab that was open last; empty when none.</summary>
        public string LastTab { get; set; } = string.Empty;

        /// <summary>The mode the window should show, taking <see cref="Modes"/> into account.</summary>
        public UiMode EffectiveMode
        {
            get
            {
                switch (Modes)
                {
                    case ModeAvailability.SimpleOnly: return UiMode.Simple;
                    case ModeAvailability.DetailOnly: return UiMode.Detail;
                    default: return Mode;
                }
            }
        }

        public bool CanSwitchMode => Modes == ModeAvailability.Both;
    }
}
