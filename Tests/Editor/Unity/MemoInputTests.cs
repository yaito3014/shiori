using NUnit.Framework;
using UnityEngine;

namespace Shiori.Editor.Tests
{
    /// <summary>Enter-to-save decision and the memo cap, kept as pure rules so they need no editor window.</summary>
    public class MemoInputTests
    {
        [Test]
        public void Enter_SavesOnlyWhenIdleAndEnabled()
        {
            Assert.That(SimpleModeView.ShouldSaveOnKey(KeyCode.Return, false, true), Is.True);
            Assert.That(SimpleModeView.ShouldSaveOnKey(KeyCode.KeypadEnter, false, true), Is.True);
            Assert.That(SimpleModeView.ShouldSaveOnKey(KeyCode.Return, true, true), Is.False, "Enter confirming an IME conversion is not a save");
            Assert.That(SimpleModeView.ShouldSaveOnKey(KeyCode.Return, false, false), Is.False, "the lock and the busy flag apply to the key too");
            Assert.That(SimpleModeView.ShouldSaveOnKey(KeyCode.Space, false, true), Is.False);
            Assert.That(SimpleModeView.ShouldSaveOnKey(KeyCode.Escape, false, true), Is.False);
        }

        [Test]
        public void FullMemo_RejectsPrintableKeysOnlyWhenNothingIsSelected()
        {
            var full = SimpleModeView.MemoMaxLength;
            Assert.That(SimpleModeView.IsRejectedByCap('a', full, false), Is.True);
            Assert.That(SimpleModeView.IsRejectedByCap('あ', full, false), Is.True);
            Assert.That(SimpleModeView.IsRejectedByCap(' ', full, false), Is.True);
            Assert.That(SimpleModeView.IsRejectedByCap('a', full, true), Is.False, "typing over a selection replaces text");
            Assert.That(SimpleModeView.IsRejectedByCap('a', full - 1, false), Is.False);
            Assert.That(SimpleModeView.IsRejectedByCap('\b', full, false), Is.False, "backspace");
            Assert.That(SimpleModeView.IsRejectedByCap('\0', full, false), Is.False, "arrow keys and modifiers carry no character");
            Assert.That(SimpleModeView.IsRejectedByCap('\n', full, false), Is.False);
        }

        [Test]
        public void MemoCap_KeepsRestoreSubjectsUnderGitHubsCut()
        {
            Assert.That(SimpleModeView.MemoMaxLength, Is.EqualTo(60));
            var longest = new string('あ', SimpleModeView.MemoMaxLength);
            Assert.That((RestoreRunner.RestoreMessagePrefix + longest).Length, Is.LessThanOrEqualTo(72));
            Assert.That(SnapshotMessage.Build(new ChangeStats(999, 999, 999)).Length, Is.LessThanOrEqualTo(72));
        }
    }
}
