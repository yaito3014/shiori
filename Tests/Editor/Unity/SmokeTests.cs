using NUnit.Framework;
using UnityEditor;

namespace Shiori.Editor.Tests
{
    public class SmokeTests
    {
        [Test]
        public void EditorTestAssemblyCompilesAndRuns()
        {
            Assert.That(EditorApplication.isPlaying, Is.False);
        }
    }
}
