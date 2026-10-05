using NUnit.Framework;

namespace Shiori.Editor.Tests
{
    public class EditorStateGuardTests
    {
        [Test]
        public void Idle_IsNotLocked()
        {
            Assert.That(EditorStateGuard.Evaluate(new EditorStateSnapshot(false, false, false, false)), Is.EqualTo(LockReason.None));
        }

        [TestCase(true, false, false, false, "Compiling")]
        [TestCase(false, true, false, false, "Updating")]
        [TestCase(false, false, true, false, "PlayMode")]
        [TestCase(false, false, false, true, "GitBusy")]
        public void SingleCondition_ReportsItsReason(bool compiling, bool updating, bool playing, bool gitBusy, string expected)
        {
            var reason = EditorStateGuard.Evaluate(new EditorStateSnapshot(compiling, updating, playing, gitBusy));
            Assert.That(reason.ToString(), Is.EqualTo(expected));
        }

        [Test]
        public void EditorConditions_TakePrecedenceOverGitActivity()
        {
            Assert.That(EditorStateGuard.Evaluate(new EditorStateSnapshot(false, false, true, true)), Is.EqualTo(LockReason.PlayMode));
            Assert.That(EditorStateGuard.Evaluate(new EditorStateSnapshot(true, true, true, true)), Is.EqualTo(LockReason.Compiling));
        }

        [Test]
        public void LiveGuard_ReflectsGitActivity()
        {
            using (var guard = new EditorStateGuard())
            {
                Assume.That(guard.Current, Is.EqualTo(LockReason.None), "the editor must be idle for this test");
                LockReason observed = LockReason.None;
                guard.Changed += r => observed = r;

                using (GitActivity.Begin("test"))
                {
                    guard.Refresh();
                    Assert.That(guard.IsLocked, Is.True);
                    Assert.That(observed, Is.EqualTo(LockReason.GitBusy));
                }

                guard.Refresh();
                Assert.That(guard.IsLocked, Is.False);
                Assert.That(observed, Is.EqualTo(LockReason.None));
            }
        }

        [Test]
        public void EveryLockReason_HasAJapaneseMessage()
        {
            foreach (LockReason reason in System.Enum.GetValues(typeof(LockReason)))
            {
                if (reason == LockReason.None) continue;
                var key = ShioriWindow.MessageKey(reason);
                Assert.That(key, Is.Not.Empty, reason.ToString());
                Assert.That(L10n.JapaneseTable.ContainsKey(key), Is.True, key);
                Assert.That(L10n.Tr(key), Is.Not.EqualTo(key), key);
            }
        }
    }
}
