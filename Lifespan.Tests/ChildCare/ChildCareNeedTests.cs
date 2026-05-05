using System.Linq;
using System.Runtime.Serialization;
using NUnit.Framework;
using Lifespan;

namespace Lifespan.Tests
{
    [TestFixture]
    public class ChildCareNeedTests
    {
        [Test]
        public void ManualMenuNeeds_PreserveLegacyLabelsAndOrder()
        {
            string[] labels = ChildCareNeeds.GetManualMenuNeeds()
                .Select(n => n.MenuLabel)
                .ToArray();

            CollectionAssert.AreEqual(
                new[]
                {
                    "Feed",
                    "Give Water",
                    "Change Diaper",
                    "Clean",
                    "Comfort (Sleep)"
                },
                labels);
        }

        [Test]
        public void NeedDefinitions_PreserveLegacyJobAndInteractionTypes()
        {
            AssertNeed(ChildCareNeeds.Feed, "feed_child_custom", "Job_FeedChild");
            AssertNeed(ChildCareNeeds.GiveWater, "give_water_child", "Job_GiveWaterChild");
            AssertNeed(ChildCareNeeds.ChangeDiaper, "change_diaper", "Job_ChangeDiaper");
            AssertNeed(ChildCareNeeds.Clean, "clean_child", "Job_CleanChild");
            AssertNeed(ChildCareNeeds.ComfortSleep, "help_sleep_child", "Job_HelpSleepChild");
        }

        [Test]
        public void ChildCareJobs_PreserveLegacyGetJobTypeValues()
        {
            Assert.AreEqual("Job_GiveWaterChild", CreateWithoutConstructor<Job_GiveWaterChild>().GetJobType());
            Assert.AreEqual("Job_ChangeDiaper", CreateWithoutConstructor<Job_ChangeDiaper>().GetJobType());
            Assert.AreEqual("Job_CleanChild", CreateWithoutConstructor<Job_CleanChild>().GetJobType());
            Assert.AreEqual("Job_HelpSleepChild", CreateWithoutConstructor<Job_HelpSleepChild>().GetJobType());
        }

        private static void AssertNeed(ChildCareNeedDefinition need, string interactionType, string jobType)
        {
            Assert.AreEqual(interactionType, need.InteractionType);
            Assert.AreEqual(jobType, need.JobType);
            Assert.AreEqual(ChildCareNeeds.DefaultMenuThreshold, need.MenuThreshold);
        }

        private static T CreateWithoutConstructor<T>()
        {
            return (T)FormatterServices.GetUninitializedObject(typeof(T));
        }
    }
}
