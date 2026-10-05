using NUnit.Framework;

namespace Shiori.Tests
{
    public class SmokeTests
    {
        [Test]
        public void CoreTestAssemblyCompilesAndRuns()
        {
            Assert.That(typeof(SmokeTests).Assembly.GetName().Name, Is.EqualTo("Shiori.Core.Tests"));
        }
    }
}
