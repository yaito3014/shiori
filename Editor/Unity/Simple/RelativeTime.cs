using System;

namespace Shiori.Editor
{
    /// <summary>"3 時間前" style labels for the history list (F3).</summary>
    internal static class RelativeTime
    {
        public static string Format(DateTimeOffset then, DateTimeOffset now)
        {
            var elapsed = now - then;
            if (elapsed < TimeSpan.Zero) elapsed = TimeSpan.Zero;

            if (elapsed.TotalMinutes < 1) return L10n.Tr("time.now");
            if (elapsed.TotalHours < 1) return L10n.Tr("time.minutes", (int)elapsed.TotalMinutes);
            if (elapsed.TotalDays < 1) return L10n.Tr("time.hours", (int)elapsed.TotalHours);
            if (elapsed.TotalDays < 30) return L10n.Tr("time.days", (int)elapsed.TotalDays);
            return L10n.Tr("time.date", then.ToLocalTime().DateTime);
        }
    }
}
