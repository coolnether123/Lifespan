using Lifespan;
using NUnit.Framework;

namespace Lifespan.Tests
{
    [TestFixture]
    public class TooltipAgeFormatterTests
    {
        [Test]
        public void Format_AppendsAgeToPlainName()
        {
            Assert.AreEqual("Ada (Age: 34)", TooltipAgeFormatter.Format("Ada", 34));
        }

        [Test]
        public void Format_ReplacesExistingAgeSuffix()
        {
            Assert.AreEqual("Ada (Age: 35)", TooltipAgeFormatter.Format("Ada (Age: 34)", 35));
        }

        [Test]
        public void GetBaseName_PreservesNonAgeParentheses()
        {
            Assert.AreEqual("Ada (Leader)", TooltipAgeFormatter.GetBaseName("Ada (Leader)"));
        }
    }
}
