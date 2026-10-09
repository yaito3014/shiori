using System;

namespace Shiori.Editor
{
    /// <summary>Lets the settings pages tell open windows that a Shiori setting changed on disk.</summary>
    internal static class ShioriSettingsEvents
    {
        public static event Action Changed;

        public static void RaiseChanged()
        {
            Changed?.Invoke();
        }
    }
}
