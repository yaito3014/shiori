using System;
using NUnit.Framework;

namespace Shiori.Editor.Tests
{
    public class RelativeTimeTests
    {
        private static readonly DateTimeOffset Now = new DateTimeOffset(2026, 10, 9, 12, 0, 0, TimeSpan.Zero);

        [TestCase(0, "たった今")]
        [TestCase(59, "たった今")]
        [TestCase(60, "1 分前")]
        [TestCase(59 * 60, "59 分前")]
        [TestCase(3 * 3600 + 10, "3 時間前")]
        [TestCase(2 * 86400, "2 日前")]
        [TestCase(29 * 86400, "29 日前")]
        public void Format_PicksTheLargestWholeUnit(int secondsAgo, string expected)
        {
            Assert.That(RelativeTime.Format(Now.AddSeconds(-secondsAgo), Now), Is.EqualTo(expected));
        }

        [Test]
        public void Format_FallsBackToTheDateAfterThirtyDays()
        {
            var then = Now.AddDays(-45);
            Assert.That(RelativeTime.Format(then, Now), Is.EqualTo(then.ToLocalTime().ToString("yyyy/MM/dd")));
        }

        [Test]
        public void Format_TreatsFutureTimesAsNow()
        {
            Assert.That(RelativeTime.Format(Now.AddMinutes(5), Now), Is.EqualTo("たった今"));
        }
    }
}
