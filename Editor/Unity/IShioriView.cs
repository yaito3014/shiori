namespace Shiori.Editor
{
    /// <summary>A main view hosted by <see cref="ShioriWindow"/>; the window drives refreshes.</summary>
    internal interface IShioriView
    {
        /// <summary>Re-read everything (history included). Used on focus and after operations.</summary>
        void RefreshAll();

        /// <summary>Re-read only the working tree. Used after project changes.</summary>
        void RefreshStatus();
    }
}
